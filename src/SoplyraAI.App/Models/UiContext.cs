namespace SoplyraAI.Models;

public sealed class UiContext
{
    public string ElementName { get; set; } = "";
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
}
