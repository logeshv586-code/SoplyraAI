using System.Runtime.CompilerServices;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

internal static class NarrativeSelfTestModule
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        if (!Environment.GetCommandLineArgs().Any(arg => arg.Equals("--self-test", StringComparison.OrdinalIgnoreCase)))
            return;

        VerifyMinimizeNarrative();
        VerifyGenericBrowserNarrative();
        VerifyUsefulDescriptionIsPreserved();
    }

    private static void VerifyMinimizeNarrative()
    {
        var step = new GuideStep
        {
            Action = "Click",
            Title = "Click Minimize",
            Context = new UiContext
            {
                ElementName = "Minimize",
                ControlType = "Button",
                LocalizedControlType = "button",
                ProcessName = "explorer",
                WindowTitle = "File Explorer"
            },
            Description = "Click the “Minimize” button to continue this task. This action is in File Explorer. The selected control is a button. Review the resulting screen before continuing to the next step."
        };

        var description = step.Description;
        if (!description.Contains("minimizes the active window", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("selected control", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("this action is in", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Meaningful Minimize description regression detected.");
    }

    private static void VerifyGenericBrowserNarrative()
    {
        var step = new GuideStep
        {
            Action = "Click",
            Title = "Click Chrome Legacy Window",
            Context = new UiContext
            {
                ElementName = "Chrome Legacy Window",
                ControlType = "Pane",
                LocalizedControlType = "pane",
                ProcessName = "chrome",
                WindowTitle = "http://192.168.1.106:8001/ - My Workspace"
            },
            Description = "Click the “Chrome Legacy Window” pane. This action is in http://192.168.1.106:8001/ - My Workspace. The selected control is a pane. Review the resulting screen before continuing to the next step."
        };

        if (!step.Title.Equals("Click highlighted area", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Generic browser title was not normalized.");
        if (!step.Context.ElementName.Equals("highlighted area", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Generic browser accessibility surface was not normalized.");

        var description = step.Description;
        if (!description.Contains("screenshot is the authoritative visual reference", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("Chrome Legacy Window", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("192.168.1.106", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("selected control is a pane", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Generic browser description regression detected.");
    }

    private static void VerifyUsefulDescriptionIsPreserved()
    {
        const string useful = "This opens the customer record so its details can be reviewed before editing.";
        var step = new GuideStep
        {
            Action = "Click",
            Title = "Click Customer",
            Context = new UiContext
            {
                ElementName = "Customer",
                ControlType = "Button",
                LocalizedControlType = "button",
                ProcessName = "ExampleApp"
            },
            Description = useful
        };

        if (!step.Description.Equals(useful, StringComparison.Ordinal))
            throw new InvalidOperationException("A useful user/AI description was overwritten.");
    }
}
