using SoplyraAI.Models;

namespace SoplyraAI.Services;

internal sealed record StepNarrative(
    string Title,
    string Instruction,
    string Purpose,
    string ExpectedResult,
    bool UsesGenericVisualTarget);

internal static class StepNarrativeService
{
    public static string NormalizeStoredDescription(
        string? action,
        UiContext? context,
        string? title,
        string? storedDescription)
    {
        context ??= new UiContext();
        var existing = PrivacySanitizer.Clean(storedDescription, 4000);
        var control = Clean(
            !string.IsNullOrWhiteSpace(context.LocalizedControlType)
                ? context.LocalizedControlType
                : context.ControlType,
            100,
            "control");
        var element = Clean(context.ElementName, 180, "");
        var application = Clean(context.ProcessName, 120, "application");
        var window = Clean(context.WindowTitle, 240, "");
        var generic = IsGenericTarget(element, control);

        if (!ShouldReplaceExistingDescription(existing, generic))
            return existing;

        var semanticText = $"{element} {title}".ToLowerInvariant();
        return PrivacySanitizer.Clean(
            BuildPurpose(semanticText, control, generic, application, window),
            2400);
    }

    public static StepNarrative Build(GuideStep step, string? documentationMode = null)
    {
        var context = step.Context ?? new UiContext();
        var action = Clean(step.Action, 40, "Click");
        var control = Clean(
            !string.IsNullOrWhiteSpace(context.LocalizedControlType)
                ? context.LocalizedControlType
                : context.ControlType,
            100,
            "control");
        var element = Clean(context.ElementName, 180, "");
        var application = Clean(context.ProcessName, 120, "application");
        var window = Clean(context.WindowTitle, 240, "");
        var generic = IsGenericTarget(element, control);

        var semanticText = $"{element} {step.Title}".ToLowerInvariant();
        var displayTarget = generic ? VisualTargetLabel(application) : element;
        var title = BuildTitle(action, element, control, generic);
        var instruction = BuildInstruction(action, displayTarget, control, generic);
        var fallbackPurpose = BuildPurpose(semanticText, control, generic, application, window);
        var existing = PrivacySanitizer.Clean(step.Description, 4000);
        var purpose = ShouldReplaceExistingDescription(existing, generic)
            ? fallbackPurpose
            : existing;
        var expected = BuildExpectedResult(semanticText + " " + purpose.ToLowerInvariant(), control, generic);

        return new StepNarrative(
            PrivacySanitizer.Clean(title, 240),
            PrivacySanitizer.Clean(instruction, 1000),
            PrivacySanitizer.Clean(purpose, 2400),
            PrivacySanitizer.Clean(expected, 1200),
            generic);
    }

    public static (string title, string description) BuildCaptureDraft(string action, UiContext context, string mode)
    {
        var step = new GuideStep
        {
            Action = action,
            Context = context,
            Title = "",
            Description = ""
        };
        var narrative = Build(step, mode);
        var description = mode.Equals("Detailed", StringComparison.OrdinalIgnoreCase)
            ? $"{narrative.Purpose} Expected result: {narrative.ExpectedResult}"
            : narrative.Purpose;
        return (narrative.Title, PrivacySanitizer.Clean(description, mode.Equals("Detailed", StringComparison.OrdinalIgnoreCase) ? 1800 : 700));
    }

    private static string BuildTitle(string action, string element, string control, bool generic)
    {
        if (!generic) return $"{action} {element}";
        if (control.Contains("pane", StringComparison.OrdinalIgnoreCase) ||
            control.Contains("window", StringComparison.OrdinalIgnoreCase))
            return $"{action} highlighted area";
        return $"{action} {HumanizeControl(control)}";
    }

    private static string BuildInstruction(string action, string target, string control, bool generic)
    {
        if (generic)
        {
            if (action.Equals("Right-click", StringComparison.OrdinalIgnoreCase))
                return $"Right-click the highlighted location shown in the captured {target}.";
            return $"Click the highlighted location shown in the captured {target}.";
        }

        var formattedTarget = control.Contains("button", StringComparison.OrdinalIgnoreCase)
            ? $"the “{target}” button"
            : control.Contains("tab", StringComparison.OrdinalIgnoreCase)
                ? $"the “{target}” tab"
                : control.Contains("menu", StringComparison.OrdinalIgnoreCase)
                    ? $"the “{target}” menu item"
                    : $"“{target}”";

        if (action.Equals("Right-click", StringComparison.OrdinalIgnoreCase))
            return $"Right-click {formattedTarget} to open its available context actions.";
        if (action.Equals("Middle-click", StringComparison.OrdinalIgnoreCase))
            return $"Middle-click {formattedTarget}.";
        if (action.Equals("Select", StringComparison.OrdinalIgnoreCase))
            return $"Select {formattedTarget}.";
        return $"Click {formattedTarget}.";
    }

    private static string BuildPurpose(string text, string control, bool generic, string application, string window)
    {
        if (ContainsAny(text, "minimize"))
            return "This minimizes the active window to the taskbar without closing it, making other windows or applications accessible.";
        if (ContainsAny(text, "maximize"))
            return "This expands the active window to use the available screen space so its controls are easier to access.";
        if (ContainsAny(text, "restore"))
            return "This returns the active window to its previous size and position.";
        if (ContainsAny(text, "save", "apply"))
            return "This saves the current changes so the entered information is retained before the workflow continues.";
        if (ContainsAny(text, "submit", "send"))
            return "This submits the current information to the application for processing.";
        if (ContainsAny(text, "add", "create", "new"))
            return "This starts creation of a new item or record in the current workflow.";
        if (ContainsAny(text, "delete", "remove", "trash"))
            return "This removes the selected item from the workflow, subject to any confirmation requested by the application.";
        if (ContainsAny(text, "search", "find"))
            return "This runs the current search or filter so matching results can be reviewed.";
        if (ContainsAny(text, "upload", "attach", "browse"))
            return "This opens file selection so a document or other file can be attached to the workflow.";
        if (ContainsAny(text, "download", "export"))
            return "This creates or downloads the requested output so it can be saved or shared.";
        if (ContainsAny(text, "next", "continue"))
            return "This advances the workflow to the next screen or stage.";
        if (ContainsAny(text, "back", "previous"))
            return "This returns the workflow to the previous screen or stage.";
        if (ContainsAny(text, "cancel"))
            return "This stops the current operation without continuing to the next stage.";
        if (ContainsAny(text, "close"))
            return "This closes the active window or dialog after the current task is complete.";
        if (ContainsAny(text, "login", "log in", "sign in"))
            return "This starts the sign-in action so the authorized workflow can continue.";
        if (ContainsAny(text, "logout", "log out", "sign out"))
            return "This signs the current user out of the application.";
        if (ContainsAny(text, "edit", "modify"))
            return "This opens the selected item for editing so its information can be changed.";
        if (ContainsAny(text, "settings", "preferences"))
            return "This opens the application settings so configuration options can be reviewed or changed.";
        if (ContainsAny(text, "copy"))
            return "This copies the selected content so it can be reused in another location.";
        if (ContainsAny(text, "refresh", "reload"))
            return "This reloads the current view so the latest information is displayed.";
        if (ContainsAny(text, "application") && !generic)
            return "This switches focus to the selected application so work can proceed there.";
        if (control.Contains("tab", StringComparison.OrdinalIgnoreCase))
            return "This switches the application to the selected section so its controls and information become available.";
        if (generic)
            return $"This activates the highlighted area shown in the captured {VisualTargetLabel(application)}. Windows did not expose a specific accessibility name for the clicked control, so the screenshot is the authoritative visual reference for this step.";
        return "This activates the selected control so the application can proceed to the next recorded state.";
    }

    private static string BuildExpectedResult(string text, string control, bool generic)
    {
        if (ContainsAny(text, "minimize"))
            return "The active window should be minimized to the taskbar while remaining open.";
        if (ContainsAny(text, "maximize"))
            return "The active window should expand to fill the available desktop area.";
        if (ContainsAny(text, "restore"))
            return "The window should return to its previous size and position.";
        if (ContainsAny(text, "save", "apply"))
            return "The application should retain the changes and keep the updated information available.";
        if (ContainsAny(text, "submit", "send"))
            return "The application should process the submission and show a confirmation, status update, or next workflow stage.";
        if (ContainsAny(text, "add", "create", "new"))
            return "A new item or entry should become available for the next part of the workflow.";
        if (ContainsAny(text, "delete", "remove", "trash"))
            return "The selected item should no longer appear after any required confirmation is completed.";
        if (ContainsAny(text, "search", "find"))
            return "The visible results should refresh to match the current search or filter criteria.";
        if (ContainsAny(text, "next", "continue"))
            return "The next screen or workflow stage should become visible.";
        if (ContainsAny(text, "back", "previous"))
            return "The previous screen or workflow stage should become visible.";
        if (ContainsAny(text, "login", "sign in", "log in"))
            return "The authorized application screen should become available if sign-in succeeds.";
        if (control.Contains("tab", StringComparison.OrdinalIgnoreCase))
            return "The selected section should become active and display its related content.";
        if (generic)
            return "The highlighted area should respond to the click and the application should show the resulting state captured in the next workflow step.";
        return "The selected control should respond and leave the application ready for the next recorded step.";
    }

    private static bool ShouldReplaceExistingDescription(string description, bool generic)
    {
        if (string.IsNullOrWhiteSpace(description)) return true;
        var normalized = description.ToLowerInvariant();
        if (normalized.Contains("the selected control is a ") ||
            normalized.Contains("review the resulting screen before continuing") ||
            normalized.Contains("to continue this task. this action is in") ||
            normalized.Contains("this action is in ") && normalized.Contains("selected control"))
            return true;
        if (generic && (normalized.Contains("chrome legacy window") || normalized.Contains("selected control is a pane")))
            return true;
        return false;
    }

    private static bool IsGenericTarget(string element, string control)
    {
        var e = element.Trim().ToLowerInvariant();
        var c = HumanizeControl(control).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(e)) return true;
        if (e == c || e == "selected control" || e == "item" || e == "pane" || e == "window" || e == "document" || e == "highlighted area") return true;
        return e.Contains("chrome legacy window") ||
               e.Contains("chrome_renderwidgethosthwnd") ||
               e.Contains("legacy window") ||
               e.Contains("render widget host") ||
               e == "application frame window";
    }

    private static string VisualTargetLabel(string application)
    {
        var app = application.ToLowerInvariant();
        if (app.Contains("chrome") || app.Contains("edge") || app.Contains("brave") || app.Contains("firefox"))
            return "browser screen";
        return "application screen";
    }

    private static string Clean(string? value, int max, string fallback)
    {
        var cleaned = PrivacySanitizer.Clean(value, max);
        return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
    }

    private static string HumanizeControl(string control) => control
        .Replace("ControlType.", "", StringComparison.OrdinalIgnoreCase)
        .Replace("_", " ")
        .Trim();

    private static bool ContainsAny(string value, params string[] tokens) =>
        tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
}