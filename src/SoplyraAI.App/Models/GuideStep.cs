using System.ComponentModel;
using System.Runtime.CompilerServices;
using SoplyraAI.Services;

namespace SoplyraAI.Models;

public sealed class GuideStep : INotifyPropertyChanged
{
    private string _title = "";
    private string _description = "";
    private string _documentationStatus = "Captured · built-in wording";

    public Guid Id { get; set; } = Guid.NewGuid();
    public int Number { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string Action { get; set; } = "Click";
    public string ScreenshotPath { get; set; } = "";
    public UiContext Context { get; set; } = new();

    // These flags are persisted with the guide so a user's wording remains authoritative
    // after app restarts, AI retries, exports, and legacy-description normalization.
    public bool TitleEditedByUser { get; set; }
    public bool DescriptionEditedByUser { get; set; }

    public string Title
    {
        get
        {
            if (TitleEditedByUser)
                return PrivacySanitizer.Clean(_title, 240);
            if (IsLegacyBrowserSurfaceTitle(_title))
                return $"{(string.IsNullOrWhiteSpace(Action) ? "Click" : Action)} highlighted area";
            return StepNarrativeService.NormalizeStoredTitle(Action, Context, _title);
        }
        set { _title = value ?? ""; OnPropertyChanged(); }
    }

    public string Description
    {
        get
        {
            if (DescriptionEditedByUser)
                return PrivacySanitizer.Clean(_description, 4000);
            return StepNarrativeService.NormalizeStoredDescription(Action, Context, _title, _description);
        }
        set { _description = value ?? ""; OnPropertyChanged(); }
    }

    public string DocumentationStatus
    {
        get => string.IsNullOrWhiteSpace(_documentationStatus) ? "Captured · built-in wording" : _documentationStatus;
        set { _documentationStatus = value ?? ""; OnPropertyChanged(); }
    }

    public void ApplyUserTitle(string? value)
    {
        _title = PrivacySanitizer.Clean(value, 240);
        TitleEditedByUser = true;
        OnPropertyChanged(nameof(Title));
    }

    public void ApplyUserDescription(string? value)
    {
        _description = PrivacySanitizer.Clean(value, 4000);
        DescriptionEditedByUser = true;
        OnPropertyChanged(nameof(Description));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static bool IsLegacyBrowserSurfaceTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.Contains("Chrome Legacy Window", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Chrome_RenderWidgetHostHWND", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("RenderWidgetHost", StringComparison.OrdinalIgnoreCase);
    }
}
