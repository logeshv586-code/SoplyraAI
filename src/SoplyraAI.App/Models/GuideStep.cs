using System.ComponentModel;
using System.Runtime.CompilerServices;
using SoplyraAI.Services;

namespace SoplyraAI.Models;

public sealed class GuideStep : INotifyPropertyChanged
{
    private string _title = "";
    private string _description = "";

    public Guid Id { get; set; } = Guid.NewGuid();
    public int Number { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string Action { get; set; } = "Click";
    public string ScreenshotPath { get; set; } = "";
    public UiContext Context { get; set; } = new();

    public string Title
    {
        get
        {
            var element = Context?.ElementName ?? "";
            if (IsGenericBrowserSurface(_title) || IsGenericBrowserSurface(element))
                return $"{(string.IsNullOrWhiteSpace(Action) ? "Click" : Action)} highlighted area";
            return _title;
        }
        set { _title = value; OnPropertyChanged(); }
    }

    public string Description
    {
        get => StepNarrativeService.NormalizeStoredDescription(Action, Context, _title, _description);
        set { _description = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static bool IsGenericBrowserSurface(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.Contains("Chrome Legacy Window", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Chrome_RenderWidgetHostHWND", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("RenderWidgetHost", StringComparison.OrdinalIgnoreCase);
    }
}
