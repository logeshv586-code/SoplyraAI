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
        VerifyGithubDesktopNarratives();
        VerifyGenericRegionTitle();
        VerifyAiQualityGate();
        VerifyWorkflowExportNaming();
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

    private static void VerifyGithubDesktopNarratives()
    {
        var repository = new GuideStep
        {
            Action = "Click",
            Title = "Click Current repository VMS",
            Context = new UiContext
            {
                ElementName = "Current repository VMS",
                ControlType = "Button",
                LocalizedControlType = "button",
                ProcessName = "GitHubDesktop"
            },
            Description = "This activates the selected control so the application can proceed to the next recorded state."
        };

        if (!repository.Description.Contains("repository selector", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Current repository description is not workflow-specific.");

        var changes = new GuideStep
        {
            Action = "Click",
            Title = "Click Changes",
            Context = new UiContext
            {
                ElementName = "Changes",
                ControlType = "Tab",
                LocalizedControlType = "tab",
                ProcessName = "GitHubDesktop"
            },
            Description = "This switches the application to the selected section so its controls and information become available."
        };

        if (!changes.Description.Contains("uncommitted file changes", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("GitHub Desktop Changes description is not specific enough.");

        var desktop = new GuideStep
        {
            Action = "Click",
            Title = "Click GitHub Desktop",
            Context = new UiContext
            {
                ElementName = "GitHub Desktop",
                ControlType = "Button",
                LocalizedControlType = "button",
                ProcessName = "GitHubDesktop"
            },
            Description = "This activates the selected control and updates the application for the next recorded step."
        };

        if (!desktop.Description.Contains("repository changes", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("GitHub Desktop focus description is not specific enough.");
    }

    private static void VerifyGenericRegionTitle()
    {
        var step = new GuideStep
        {
            Action = "Click",
            Title = "Click region",
            Context = new UiContext
            {
                ElementName = "region",
                ControlType = "Region",
                LocalizedControlType = "region",
                ProcessName = "GitHubDesktop"
            }
        };

        if (!step.Title.Equals("Click highlighted area", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Generic region title was not normalized to a visual target.");
    }

    private static void VerifyAiQualityGate()
    {
        var session = new GuideSession { Title = "GitHub Flow", DocumentationMode = "Detailed" };
        var step = new GuideStep
        {
            Action = "Click",
            Title = "Click Current repository VMS",
            Context = new UiContext
            {
                ElementName = "Current repository VMS",
                ControlType = "Button",
                LocalizedControlType = "button",
                ProcessName = "GitHubDesktop"
            },
            Description = "This activates the selected control so the application can proceed to the next recorded state."
        };
        session.Steps.Add(step);

        var settings = new AppSettings
        {
            HasCompletedAiSetup = true,
            AiProvider = "Ollama",
            AiModel = "qwen3:4b",
            SendScreenshotsToAi = false,
            DocumentationMode = "Detailed"
        };

        var weak = AiDescriptionQualityService.Resolve(
            step,
            session,
            "This activates the selected control so the application can proceed to the next recorded state.",
            settings);
        if (weak.UsedAi || !weak.Text.Contains("repository selector", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Weak model output replaced the grounded repository description.");

        var strong = AiDescriptionQualityService.Resolve(
            step,
            session,
            "This opens the Current repository selector in GitHub Desktop so the user can switch the active repository for subsequent actions.",
            settings);
        if (!strong.UsedAi || !strong.Text.Contains("Current repository", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A grounded model description was rejected unexpectedly.");
    }

    private static void VerifyWorkflowExportNaming()
    {
        var root = Path.Combine(Path.GetTempPath(), "soplyraai-name-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var generated = Path.Combine(root, "guide.pdf");
            File.WriteAllText(generated, "test");
            var session = new GuideSession { Title = "GitHub Flow" };
            var renamed = ExportFileNaming.RenameGeneratedFile(session, generated);

            if (!Path.GetFileName(renamed).Equals("GitHub Flow.pdf", StringComparison.Ordinal))
                throw new InvalidOperationException("Workflow title was not used as the exported PDF filename.");
            if (!File.Exists(renamed) || File.Exists(generated))
                throw new InvalidOperationException("Export file rename did not complete safely.");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
