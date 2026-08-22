using System.Runtime.CompilerServices;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

internal static class CaptureEditorSelfTestModule
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        if (!Environment.GetCommandLineArgs().Any(arg => arg.Equals("--self-test", StringComparison.OrdinalIgnoreCase)))
            return;

        VerifyPromptEchoIsRejected();
        VerifyGroundedNamedControlIsAccepted();
        VerifyManualDescriptionWins();
    }

    private static GuideStep CreateDiscordStep() => new()
    {
        Action = "Click",
        Title = "Click Discord",
        Context = new UiContext
        {
            ElementName = "Discord",
            ControlType = "Hyperlink",
            LocalizedControlType = "link",
            ProcessName = "chrome",
            WindowTitle = "Discord"
        },
        Description = "This activates the selected control so the application can move to the resulting workflow state."
    };

    private static AppSettings CreateSettings() => new()
    {
        EnableAi = true,
        HasCompletedAiSetup = true,
        AiProvider = "Ollama",
        AiModel = "qwen3:1.7b",
        SendScreenshotsToAi = false,
        DocumentationMode = "Quick"
    };

    private static void VerifyPromptEchoIsRejected()
    {
        var step = CreateDiscordStep();
        var session = new GuideSession { Title = "Discord flow", DocumentationMode = "Quick" };
        session.Steps.Add(step);

        var reasoning = "We are given: Action: Click Element: Discord Control type: Hyperlink. The instruction says to explain what this does. We need to write the answer from the context.";
        var decision = AiDescriptionQualityService.Resolve(step, session, reasoning, CreateSettings());

        if (decision.UsedAi ||
            !decision.Text.Contains("Discord", StringComparison.OrdinalIgnoreCase) ||
            decision.Text.Contains("activates the selected control", StringComparison.OrdinalIgnoreCase) ||
            decision.Text.Contains("we are given", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Prompt-echo AI output was not safely replaced by the grounded named-control fallback.");
    }

    private static void VerifyGroundedNamedControlIsAccepted()
    {
        var step = CreateDiscordStep();
        var session = new GuideSession { Title = "Discord flow", DocumentationMode = "Quick" };
        session.Steps.Add(step);

        const string grounded = "This opens the Discord destination so the linked Discord page becomes available in the browser.";
        var decision = AiDescriptionQualityService.Resolve(step, session, grounded, CreateSettings());

        if (!decision.UsedAi || !decision.Text.Contains("Discord", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A concise grounded named-control description was rejected unexpectedly.");
    }

    private static void VerifyManualDescriptionWins()
    {
        var step = CreateDiscordStep();
        step.ApplyUserDescription("Open the team Discord workspace used for this workflow.");
        var session = new GuideSession { Title = "Discord flow", DocumentationMode = "Quick" };
        session.Steps.Add(step);

        var decision = AiDescriptionQualityService.Resolve(
            step,
            session,
            "This opens Discord and replaces the user's wording.",
            CreateSettings());

        if (decision.UsedAi ||
            !decision.Text.Equals("Open the team Discord workspace used for this workflow.", StringComparison.Ordinal) ||
            !step.DescriptionEditedByUser)
            throw new InvalidOperationException("Manual step wording was not preserved as authoritative text.");
    }
}
