namespace SoplyraAI.Models;

public sealed class UiContext
{
    private string _elementName = "";

    public string ElementName
    {
        get => IsGenericBrowserSurface(_elementName) ? "highlighted area" : _elementName;
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

    private static bool IsGenericBrowserSurface(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.Contains("Chrome Legacy Window", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Chrome_RenderWidgetHostHWND", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("RenderWidgetHost", StringComparison.OrdinalIgnoreCase);
    }
}
