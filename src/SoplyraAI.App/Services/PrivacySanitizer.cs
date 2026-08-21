using System.Text.RegularExpressions;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

public static class PrivacySanitizer
{
    private const string Redacted = "[redacted-secret]";

    private static readonly Regex[] SecretPatterns =
    {
        new(@"\bBearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled),
        new(@"\bsk-[A-Za-z0-9_-]{16,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled),
        new(@"\bgh[pousr]_[A-Za-z0-9]{20,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled),
        new(@"\bAKIA[0-9A-Z]{16}\b", RegexOptions.CultureInvariant | RegexOptions.Compiled),
        new(@"\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b", RegexOptions.CultureInvariant | RegexOptions.Compiled)
    };

    public static void SanitizeContext(UiContext context)
    {
        context.ElementName = Clean(context.ElementName, 160);
        context.AutomationId = Clean(context.AutomationId, 160);
        context.ControlType = Clean(context.ControlType, 80);
        context.ClassName = Clean(context.ClassName, 160);
        context.HelpText = Clean(context.HelpText, 240);
        context.LocalizedControlType = Clean(context.LocalizedControlType, 80);
        context.WindowTitle = Clean(context.WindowTitle, 240);
        context.ProcessName = Clean(context.ProcessName, 120);

        if (context.IsPassword)
        {
            context.ElementName = "Sensitive field";
            context.AutomationId = "";
            context.HelpText = "";
        }
    }

    public static string Clean(string? value, int maxLength = 240)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        var chars = value
            .Where(ch => !char.IsControl(ch) || ch is '\t' or '\r' or '\n')
            .Select(ch => ch is '\t' or '\r' or '\n' ? ' ' : ch)
            .ToArray();

        var text = string.Join(" ", new string(chars)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

        foreach (var pattern in SecretPatterns)
            text = pattern.Replace(text, Redacted);

        if (maxLength < 1) return "";
        return text.Length <= maxLength ? text : text[..maxLength] + "…";
    }
}
