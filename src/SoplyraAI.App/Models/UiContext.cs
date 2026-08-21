namespace SoplyraAI.Models;

public sealed class UiContext
{
    private string _elementName = "";

    public string ElementName
    {
        get => IsFrameworkOnlyName(_elementName, ControlType, LocalizedControlType) ? "highlighted area" : _elementName;
        set => _elementName = value ?? "";
    }

    public string AutomationId { get; set; } = "";
    public string ControlType { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string HelpText { get; set; } = "";
    public string LocalizedControlType { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public int ProcessId { get; set; }
    public bool IsPassword { get; set; }
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int ClickX { get; set; }
    public int ClickY { get; set; }

    private static bool IsFrameworkOnlyName(string? value, string? controlType, string? localizedControlType)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim().ToLowerInvariant();
        var type = (string.IsNullOrWhiteSpace(localizedControlType) ? controlType : localizedControlType) ?? "";
        type = type.Replace("ControlType.", "", StringComparison.OrdinalIgnoreCase)
                   .Replace("_", " ")
                   .Trim()
                   .ToLowerInvariant();

        if (normalized.Contains("chrome legacy window") ||
            normalized.Contains("chrome_renderwidgethosthwnd") ||
            normalized.Contains("renderwidgethost"))
            return true;

        if (normalized is "region" or "group" or "pane" or "window" or "document" or "item" or "control")
            return true;

        return !string.IsNullOrWhiteSpace(type) && normalized.Equals(type, StringComparison.OrdinalIgnoreCase) &&
               normalized is "region" or "group" or "pane" or "window" or "document" or "item";
    }
}
