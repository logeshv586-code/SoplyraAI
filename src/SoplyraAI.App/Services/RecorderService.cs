using System.Diagnostics;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

public sealed class RecorderService : IDisposable
{
    private readonly GlobalMouseHook _mouse = new();
    private readonly UiAutomationService _ui = new();
    private readonly ScreenshotService _screens = new();
    private readonly DescriptionService _describer = new();
    private readonly SessionStore _store;
    private readonly AppSettings _settings;
    private GuideSession? _session;
    private bool _paused;
    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private DateTimeOffset _lastCapture = DateTimeOffset.MinValue;
    private int _lastX, _lastY;

    public event EventHandler<GuideStep>? StepCaptured;

    public RecorderService(SessionStore store, AppSettings settings)
    {
        _store = store;
        _settings = settings;
        _mouse.MouseAction += OnMouseAction;
    }

    public bool IsRecording => _session is not null;
    public bool IsPaused => _paused;

    public void Start(GuideSession session)
    {
        if (_session is not null) return;
        _session = session;
        _paused = false;
        _mouse.Start();
    }

    public void TogglePause() => _paused = !_paused;

    public void Stop()
    {
        _mouse.Stop();
        if (_session is not null) _store.Save(_session);
        _session = null;
        _paused = false;
    }

    private void OnMouseAction(object? sender, MouseActionEventArgs e)
    {
        if (_paused || _session is null) return;
        _ = CaptureAsync(e);
    }

    private async Task CaptureAsync(MouseActionEventArgs e)
    {
        if (_session is null) return;
        await _captureGate.WaitAsync();
        try
        {
            var now = DateTimeOffset.Now;
            if ((now - _lastCapture).TotalMilliseconds < 120 && Math.Abs(e.X - _lastX) < 5 && Math.Abs(e.Y - _lastY) < 5)
                return;

            _lastCapture = now;
            _lastX = e.X;
            _lastY = e.Y;

            var context = _ui.Capture(e.X, e.Y);
            PrivacySanitizer.SanitizeContext(context);
            if (context.ProcessId == Environment.ProcessId || IsShellSurface(context)) return;

            await Task.Delay(Math.Clamp(_settings.CaptureDelayMs, 0, 1000));
            if (_session is null) return;

            var number = _session.Steps.Count + 1;
            var requestedImage = Path.Combine(_session.SessionFolder, "images", $"step-{number:000}-{Guid.NewGuid():N}.png");
            if (!PathSecurity.TryGetTrustedPng(_session.SessionFolder, requestedImage, out var trustedTarget, requireExists: false)) return;

            _screens.Capture(trustedTarget, context, _settings.ScreenshotMode);
            if (!PathSecurity.TryGetTrustedPng(_session.SessionFolder, trustedTarget, out var trustedImage, requireExists: true)) return;

            var text = _describer.DescribeFast(e.Action, context, _session.DocumentationMode);
            var step = new GuideStep
            {
                Number = number,
                Action = PrivacySanitizer.Clean(e.Action, 40),
                ScreenshotPath = trustedImage,
                Context = context,
                Title = text.title,
                Description = text.description
            };

            _session.Steps.Add(step);
            _store.Save(_session);
            StepCaptured?.Invoke(this, step);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(PrivacySanitizer.Clean(ex.Message, 500));
        }
        finally { _captureGate.Release(); }
    }

    private static bool IsShellSurface(UiContext c) =>
        c.ClassName.Equals("Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
        c.ClassName.Equals("Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        _mouse.MouseAction -= OnMouseAction;
        _mouse.Dispose();
        _captureGate.Dispose();
    }
}
