using System.Windows;
using System.Windows.Interop;
using SoplyraAI.Services;

namespace SoplyraAI.Views;

public partial class CaptureBarWindow : Window
{
    public event EventHandler? StopRequested;
    public event EventHandler? PauseRequested;
    private bool _paused;
    private IntPtr _windowHandle;

    public bool IsExcludedFromCapture { get; private set; }

    public CaptureBarWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyCaptureExclusion();
        Loaded += (_, _) =>
        {
            Left = SystemParameters.WorkArea.Left + 18;
            Top = SystemParameters.WorkArea.Top + 18;
            ApplyCaptureExclusion();
        };
        Closed += (_, _) =>
        {
            if (_windowHandle != IntPtr.Zero)
                CaptureOverlayRegistry.Clear(_windowHandle);
        };
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove(); };
    }

    public void SetStepCount(int count) => StepText.Text = $"{count} step{(count == 1 ? "" : "s")} captured";

    private void ApplyCaptureExclusion()
    {
        try
        {
            _windowHandle = new WindowInteropHelper(this).Handle;
            if (_windowHandle == IntPtr.Zero) return;
            IsExcludedFromCapture = NativeMethods.SetWindowDisplayAffinity(_windowHandle, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
            CaptureOverlayRegistry.Register(_windowHandle, IsExcludedFromCapture);
        }
        catch
        {
            IsExcludedFromCapture = false;
            if (_windowHandle != IntPtr.Zero)
                CaptureOverlayRegistry.Register(_windowHandle, excludedByWindows: false);
        }
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        _paused = !_paused;
        PauseButton.Content = _paused ? "Resume" : "Pause";
        PauseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void StopButton_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke(this, EventArgs.Empty);
}
