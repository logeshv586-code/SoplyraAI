using SoplyraAI.Models;

namespace SoplyraAI.Services;

internal static class GroundedFallbackDescriptionService
{
    public static string Build(GuideStep step, string baseline)
    {
        ArgumentNullException.ThrowIfNull(step);
        var safeBaseline = PrivacySanitizer.Clean(baseline, 2400);
        if (!LooksWeak(safeBaseline)) return safeBaseline;

        var context = step.Context ?? new UiContext();
        if (StepNarrativeService.IsGenericContext(context)) return safeBaseline;

        var name = PrivacySanitizer.Clean(context.ElementName, 180).Trim();
        if (string.IsNullOrWhiteSpace(name)) return safeBaseline;

        var control = PrivacySanitizer.Clean(
            !string.IsNullOrWhiteSpace(context.LocalizedControlType)
                ? context.LocalizedControlType
                : context.ControlType,
            100).Trim().ToLowerInvariant();

        if (TryGetBrowserTarget(name, out var browserTarget))
            return $"This opens {browserTarget} in the browser so the workflow can continue there.";

        if (control.Contains("hyperlink", StringComparison.OrdinalIgnoreCase) ||
            control.Equals("link", StringComparison.OrdinalIgnoreCase))
            return $"This opens the “{name}” destination so its linked page or content becomes available.";

        if (control.Contains("tab", StringComparison.OrdinalIgnoreCase))
            return $"This switches to the “{name}” section so its controls and information become available.";

        if (control.Contains("menu", StringComparison.OrdinalIgnoreCase))
            return $"This opens the “{name}” menu so its available actions can be selected.";

        if (control.Contains("check", StringComparison.OrdinalIgnoreCase) ||
            control.Contains("radio", StringComparison.OrdinalIgnoreCase))
            return $"This changes the “{name}” option so the selected setting is applied to the workflow.";

        if (control.Contains("button", StringComparison.OrdinalIgnoreCase))
            return $"This runs the “{name}” action so the application can show the resulting workflow state.";

        return $"This selects “{name}” so the workflow can continue with that choice.";
    }

    private static bool LooksWeak(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        return value.Contains("activates the selected control", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("resulting workflow state", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("next recorded state", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("updates the application for the next recorded step", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetBrowserTarget(string name, out string target)
    {
        target = "";
        const string prefix = "Open ";
        const string suffix = " in your browser";
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return false;

        var length = name.Length - prefix.Length - suffix.Length;
        if (length <= 0) return false;
        target = PrivacySanitizer.Clean(name.Substring(prefix.Length, length), 120).Trim();
        return !string.IsNullOrWhiteSpace(target);
    }
}
