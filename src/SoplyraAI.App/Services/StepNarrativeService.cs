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
    public static string NormalizeStoredTitle(
        string? action,
        UiContext? context,
        string? storedTitle)
    {
        context ??= new UiContext();
        var verb = Clean(action, 40, "Click");
        var title = Clean(storedTitle, 240, "");
        var control = Clean(
            !string.IsNullOrWhiteSpace(context.LocalizedControlType)
                ? context.LocalizedControlType
                : context.ControlType,
            100,
            "control");
        var element = Clean(context.ElementName, 180, "");

        if (!IsGenericTarget(element, control))
            return string.IsNullOrWhiteSpace(title) ? $"{verb} {element}" : title;

        if (string.IsNullOrWhiteSpace(title) || LooksLikeGenericTitle(title, element, control))
            return control.Contains("button", StringComparison.OrdinalIgnoreCase)
                ? $"{verb} highlighted button"
                : control.Contains("tab", StringComparison.OrdinalIgnoreCase)
                    ? $"{verb} highlighted tab"
                    : control.Contains("menu", StringComparison.OrdinalIgnoreCase)
                        ? $"{verb} highlighted menu item"
                        : $"{verb} highlighted area";

        return title;
    }

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
        var helpText = Clean(context.HelpText, 240, "");
        var generic = IsGenericTarget(element, control);

        if (!ShouldReplaceExistingDescription(existing, generic))
            return existing;

        var semanticText = BuildSemanticText(element, title, helpText);
        return PrivacySanitizer.Clean(
            BuildPurpose(semanticText, control, generic, application),
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
        var helpText = Clean(context.HelpText, 240, "");
        var generic = IsGenericTarget(element, control);
        var semanticText = BuildSemanticText(element, step.Title, helpText);

        var displayTarget = generic ? VisualTargetLabel(application) : element;
        var title = NormalizeStoredTitle(action, context, step.Title);
        var instruction = BuildInstruction(action, displayTarget, control, generic);
        var fallbackPurpose = BuildPurpose(semanticText, control, generic, application);
        var existing = PrivacySanitizer.Clean(step.Description, 4000);
        var purpose = ShouldReplaceExistingDescription(existing, generic)
            ? fallbackPurpose
            : existing;
        var expected = BuildExpectedResult(semanticText, purpose, control, generic, application);

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
        // Description stores only WHAT THIS DOES. Expected result is generated separately
        // during export, so it must not be mixed into this field.
        return (
            narrative.Title,
            PrivacySanitizer.Clean(narrative.Purpose, mode.Equals("Detailed", StringComparison.OrdinalIgnoreCase) ? 1800 : 700));
    }

    public static bool IsGenericContext(UiContext? context)
    {
        context ??= new UiContext();
        var control = Clean(
            !string.IsNullOrWhiteSpace(context.LocalizedControlType)
                ? context.LocalizedControlType
                : context.ControlType,
            100,
            "control");
        return IsGenericTarget(Clean(context.ElementName, 180, ""), control);
    }

    private static string BuildSemanticText(string element, string? title, string helpText) =>
        $"{element} {title} {helpText}".ToLowerInvariant();

    private static string BuildInstruction(string action, string target, string control, bool generic)
    {
        if (generic)
        {
            var visual = control.Contains("button", StringComparison.OrdinalIgnoreCase)
                ? "highlighted button"
                : control.Contains("tab", StringComparison.OrdinalIgnoreCase)
                    ? "highlighted tab"
                    : control.Contains("menu", StringComparison.OrdinalIgnoreCase)
                        ? "highlighted menu item"
                        : "highlighted location";

            if (action.Equals("Right-click", StringComparison.OrdinalIgnoreCase))
                return $"Right-click the {visual} shown in the captured {target}.";
            if (action.Equals("Middle-click", StringComparison.OrdinalIgnoreCase))
                return $"Middle-click the {visual} shown in the captured {target}.";
            return $"Click the {visual} shown in the captured {target}.";
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

    private static string BuildPurpose(string text, string control, bool generic, string application)
    {
        // Window controls.
        if (ContainsAny(text, "minimize"))
            return "This minimizes the active window to the taskbar without closing it, making other windows or applications accessible.";
        if (ContainsAny(text, "maximize"))
            return "This expands the active window to use the available screen space so its controls are easier to access.";
        if (ContainsAny(text, "restore"))
            return "This returns the active window to its previous size and position.";

        // Git/repository workflows. These labels are exposed directly by GitHub Desktop
        // and similar clients, so we can document their purpose without asking a model to guess.
        if (ContainsAny(text, "current repository", "choose repository", "select repository"))
            return "This opens the repository selector so you can choose or switch the active repository for the workflow.";
        if (ContainsAny(text, "current branch", "choose branch", "select branch"))
            return "This opens the branch selector so you can choose the branch that should be active for the repository.";
        if (ContainsAny(text, "fetch origin", "fetch upstream"))
            return "This checks the configured remote for new commits and updates the local remote-tracking information without changing local work.";
        if (ContainsAny(text, "pull origin", "pull upstream"))
            return "This downloads available remote commits and integrates them into the active local branch.";
        if (ContainsAny(text, "push origin", "push upstream"))
            return "This publishes local commits from the active branch to the configured remote repository.";
        if (ContainsAny(text, "create pull request", "new pull request"))
            return "This starts the pull-request flow for proposing the active branch changes for review and merge.";
        if (ContainsAny(text, "updating submodule", "update submodule", "submodules"))
            return "This selects the repository submodule status/action so its update progress or details can be reviewed.";
        if (ContainsAny(text, "discard changes", "discard all changes"))
            return "This starts the action for discarding uncommitted changes, subject to any confirmation shown by the application.";
        if (ContainsAny(text, "changes") && !ContainsAny(text, "discard changes"))
            return "This switches to the Changes view, where uncommitted file changes can be reviewed and prepared for a commit.";
        if (ContainsAny(text, "history"))
            return "This switches to the History view so previous commits and repository activity can be reviewed.";
        if (ContainsAny(text, "show in explorer", "open in explorer", "show in finder"))
            return "This opens the repository folder in the operating system file manager.";
        if (ContainsAny(text, "open in visual studio code", "open in vscode", "open in code"))
            return "This opens the current repository in Visual Studio Code for file review or editing.";
        if (ContainsAny(text, "github desktop"))
            return "This brings GitHub Desktop into focus so repository changes, branches, and synchronization actions can be managed.";
        if (IsViewMenu(text, control))
            return "This opens the View menu so navigation and interface display commands can be selected.";
        if (IsRepositoryMenu(text, control))
            return "This opens the Repository menu so repository-specific commands and settings can be selected.";
        if (IsBranchMenu(text, control))
            return "This opens the Branch menu so branch-related commands can be selected.";

        // Common application actions.
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
        if (control.Contains("tab", StringComparison.OrdinalIgnoreCase))
            return "This switches the application to the selected section so its controls and information become available.";

        if (generic)
            return $"This activates the highlighted area shown in the captured {VisualTargetLabel(application)}. Windows did not expose a specific accessibility name for the clicked control, so the screenshot is the authoritative visual reference for this step.";

        return "This activates the selected control so the application can move to the resulting workflow state.";
    }

    private static string BuildExpectedResult(string text, string purpose, string control, bool generic, string application)
    {
        var combined = $"{text} {purpose}".ToLowerInvariant();

        if (ContainsAny(combined, "minimize"))
            return "The active window should be minimized to the taskbar while remaining open.";
        if (ContainsAny(combined, "maximize"))
            return "The active window should expand to fill the available desktop area.";
        if (ContainsAny(combined, "restore"))
            return "The window should return to its previous size and position.";
        if (ContainsAny(combined, "current repository", "repository selector"))
            return "A repository list or selector should become available so a different repository can be chosen if required.";
        if (ContainsAny(combined, "current branch", "branch selector"))
            return "A branch list or selector should become available so the active branch can be changed if required.";
        if (ContainsAny(combined, "fetch origin", "fetch upstream"))
            return "The remote status should refresh and any newly discovered remote commits should be reflected in the repository state.";
        if (ContainsAny(combined, "pull origin", "pull upstream"))
            return "The active branch should update with the pulled remote commits unless the application reports a conflict or other blocking condition.";
        if (ContainsAny(combined, "push origin", "push upstream"))
            return "The remote repository should receive the local commits and the synchronization status should update.";
        if (ContainsAny(combined, "pull-request", "pull request"))
            return "The pull-request creation flow should open with the active repository and branch context available.";
        if (ContainsAny(combined, "changes view", "uncommitted file changes"))
            return "The Changes view should become active and display the current uncommitted file changes for the repository.";
        if (ContainsAny(combined, "history view", "previous commits"))
            return "The History view should become active and display commit history for the current repository.";
        if (ContainsAny(combined, "view menu"))
            return "The View menu should open and show the available navigation or display commands.";
        if (ContainsAny(combined, "repository menu"))
            return "The Repository menu should open and show repository-specific commands.";
        if (ContainsAny(combined, "branch menu"))
            return "The Branch menu should open and show branch-related commands.";
        if (ContainsAny(combined, "github desktop"))
            return "GitHub Desktop should be active and ready for the next repository action.";
        if (ContainsAny(combined, "submodule"))
            return "The submodule status or related details should remain visible so update progress can be reviewed.";
        if (ContainsAny(combined, "save", "apply"))
            return "The application should retain the changes and keep the updated information available.";
        if (ContainsAny(combined, "submit", "send"))
            return "The application should process the submission and show a confirmation, status update, or next workflow stage.";
        if (ContainsAny(combined, "add", "create", "new"))
            return "A new item or entry should become available for the next part of the workflow.";
        if (ContainsAny(combined, "delete", "remove", "trash"))
            return "The selected item should no longer appear after any required confirmation is completed.";
        if (ContainsAny(combined, "search", "find"))
            return "The visible results should refresh to match the current search or filter criteria.";
        if (ContainsAny(combined, "next", "continue"))
            return "The next screen or workflow stage should become visible.";
        if (ContainsAny(combined, "back", "previous"))
            return "The previous screen or workflow stage should become visible.";
        if (ContainsAny(combined, "login", "sign in", "log in"))
            return "The authorized application screen should become available if sign-in succeeds.";
        if (control.Contains("tab", StringComparison.OrdinalIgnoreCase))
            return "The selected section should become active and display its related content.";
        if (generic)
            return $"The highlighted area should respond to the click and the captured {VisualTargetLabel(application)} should show the resulting state.";
        return "The selected control should respond and leave the application in the resulting state for the next recorded step.";
    }

    private static bool ShouldReplaceExistingDescription(string description, bool generic)
    {
        if (string.IsNullOrWhiteSpace(description)) return true;
        var normalized = description.ToLowerInvariant();

        if (normalized.Contains("the selected control is a ") ||
            normalized.Contains("review the resulting screen before continuing") ||
            normalized.Contains("to continue this task. this action is in") ||
            normalized.Contains("this action is in ") && normalized.Contains("selected control") ||
            normalized.Contains("this activates the selected control so the application can proceed") ||
            normalized.Contains("this activates the selected control and updates the application") ||
            normalized.Contains("this activates the selected control so the application can move") ||
            normalized.Contains("the interface should respond to the selected control") ||
            normalized.Equals("this switches the application to the selected section so its controls and information become available.", StringComparison.Ordinal))
            return true;

        if (generic && (normalized.Contains("chrome legacy window") ||
                        normalized.Contains("selected control is a pane") ||
                        normalized.Contains("selected control is a region") ||
                        normalized.Contains("selected control is a group")))
            return true;

        return false;
    }

    private static bool LooksLikeGenericTitle(string title, string element, string control)
    {
        var normalized = title.Trim().ToLowerInvariant();
        var e = element.Trim().ToLowerInvariant();
        var c = HumanizeControl(control).Trim().ToLowerInvariant();
        if (normalized is "click region" or "click group" or "click pane" or "click document" or "click window" or "click highlighted area")
            return true;
        return (!string.IsNullOrWhiteSpace(e) && normalized.EndsWith(e, StringComparison.Ordinal)) ||
               (!string.IsNullOrWhiteSpace(c) && normalized.EndsWith(c, StringComparison.Ordinal));
    }

    private static bool IsGenericTarget(string element, string control)
    {
        var e = element.Trim().ToLowerInvariant();
        var c = HumanizeControl(control).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(e)) return true;
        if (e == c || e is "selected control" or "item" or "pane" or "window" or "document" or "region" or "group" or "highlighted area") return true;
        return e.Contains("chrome legacy window") ||
               e.Contains("chrome_renderwidgethosthwnd") ||
               e.Contains("legacy window") ||
               e.Contains("render widget host") ||
               e == "application frame window";
    }

    private static bool IsViewMenu(string text, string control) =>
        ContainsAny(text, "click view", "view menu", " view ") && control.Contains("menu", StringComparison.OrdinalIgnoreCase);

    private static bool IsRepositoryMenu(string text, string control) =>
        ContainsAny(text, "repository menu", "click repository") && control.Contains("menu", StringComparison.OrdinalIgnoreCase);

    private static bool IsBranchMenu(string text, string control) =>
        ContainsAny(text, "branch menu", "click branch") && control.Contains("menu", StringComparison.OrdinalIgnoreCase);

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
