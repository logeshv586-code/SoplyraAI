using System.Diagnostics;
using System.Text;
using System.Windows.Automation;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

public sealed class UiAutomationService
{
    public UiContext Capture(int x, int y)
    {
        var context = new UiContext { ClickX = x, ClickY = y };
        try
        {
            var element = AutomationElement.FromPoint(new System.Windows.Point(x, y));
            if (element is not null)
            {
                context.ElementName = Safe(() => element.Current.Name);
                context.AutomationId = Safe(() => element.Current.AutomationId);
                context.ClassName = Safe(() => element.Current.ClassName);
                context.HelpText = Safe(() => element.Current.HelpText);
                context.LocalizedControlType = Safe(() => element.Current.LocalizedControlType);
                context.ControlType = Safe(() => element.Current.ControlType?.ProgrammaticName?.Replace("ControlType.", "")) ?? "";
                context.ProcessId = SafeInt(() => element.Current.ProcessId);
                context.IsPassword = SafeBool(() => element.Current.IsPassword);
                var rect = SafeRect(() => element.Current.BoundingRectangle);
                context.Left = rect.Left;
                context.Top = rect.Top;
                context.Width = rect.Width;
                context.Height = rect.Height;
            }
        }
        catch { }

        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd != IntPtr.Zero)
            {
                var title = new StringBuilder(512);
                NativeMethods.GetWindowText(hwnd, title, title.Capacity);
                context.WindowTitle = title.ToString();
                NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
                if (context.ProcessId == 0) context.ProcessId = unchecked((int)pid);
            }
            if (context.ProcessId > 0)
                context.ProcessName = Process.GetProcessById(context.ProcessId).ProcessName;
        }
        catch { }

        return context;
    }

    private static string Safe(Func<string> getter) { try { return getter() ?? ""; } catch { return ""; } }
    private static int SafeInt(Func<int> getter) { try { return getter(); } catch { return 0; } }
    private static bool SafeBool(Func<bool> getter) { try { return getter(); } catch { return false; } }
    private static System.Windows.Rect SafeRect(Func<System.Windows.Rect> getter) { try { return getter(); } catch { return System.Windows.Rect.Empty; } }
}
