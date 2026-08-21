using System.Text.Json;
using System.Text.RegularExpressions;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

internal sealed record AiDescriptionDecision(string Text, bool UsedAi, string Reason);

internal static partial class AiDescriptionQualityService
{
    private static readonly string[] WeakPhrases =
    {
        "activates the selected control",
        "updates the application for the next recorded step",
        "application can proceed to the next recorded state",
        "review the resulting screen before continuing",
        "the selected control is a",
        "this action is in",
        "continue this task",
        "as an ai",
        "i cannot determine",
        "i can't determine",
        "cannot determine from the provided",
        "not enough context",
        "insufficient context",
        "it is unclear",
        "appears to",
        "seems to",
        "probably",
        "maybe"
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "click", "select", "open", "button", "tab", "menu", "item", "control", "current",
        "the", "this", "that", "with", "from", "into", "area", "highlighted", "application",
        "screen", "window", "document", "group", "region", "pane", "desktop"
    };

    public static AiDescriptionDecision Resolve(
        GuideStep step,
        GuideSession session,
        string? modelOutput,
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(settings);

        var baseline = StepNarrativeService.Build(step, session.DocumentationMode).Purpose;
        var candidate = NormalizeCandidate(modelOutput);
        if (string.IsNullOrWhiteSpace(candidate))
            return new AiDescriptionDecision(baseline, false, "Model returned no usable description.");

        if (candidate.Length < 28)
            return new AiDescriptionDecision(baseline, false, "Model description was too short to be reliable.");

        if (WeakPhrases.Any(phrase => candidate.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
            return new AiDescriptionDecision(baseline, false, "Model description was generic, uncertain, or boilerplate.");

        if (LooksLikeInstructionInsteadOfPurpose(candidate))
            return new AiDescriptionDecision(baseline, false, "Model repeated the click instruction instead of explaining the control purpose.");

        var hasVisionEvidence = settings.SendScreenshotsToAi &&
                                AiProviderCatalog.IsVisionModel(settings.AiProvider, settings.AiModel);
        var genericContext = StepNarrativeService.IsGenericContext(step.Context);

        if (genericContext && !hasVisionEvidence)
            return new AiDescriptionDecision(
                baseline,
                false,
                "Windows exposed only a generic control and the configured model did not receive screenshot vision evidence.");

        if (!genericContext && !ContainsGroundingAnchor(step, candidate))
            return new AiDescriptionDecision(
                baseline,
                false,
                "Model description did not reference the captured control strongly enough.");

        if (genericContext && hasVisionEvidence && !LooksSpecificEnoughForVision(candidate))
            return new AiDescriptionDecision(
                baseline,
                false,
                "Vision model output did not identify a specific visible control or workflow purpose.");

        return new AiDescriptionDecision(
            PrivacySanitizer.Clean(candidate, session.DocumentationMode == "Detailed" ? 1200 : 700),
            true,
            "Grounded model description accepted.");
    }

    private static string NormalizeCandidate(string? value)
    {
        var text = PrivacySanitizer.Clean(value, 4000).Trim();
        if (string.IsNullOrWhiteSpace(text)) return "";

        text = text.Replace("```json", "", StringComparison.OrdinalIgnoreCase)
                   .Replace("```", "", StringComparison.Ordinal)
                   .Trim();

        if (text.StartsWith('{') && text.EndsWith('}'))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                foreach (var key in new[] { "purpose", "what_this_does", "description", "whatThisDoes" })
                {
                    if (doc.RootElement.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String)
                    {
                        text = property.GetString() ?? "";
                        break;
                    }
                }
            }
            catch
            {
                // Some smaller local models emit JSON-like text. Continue with the plain-text guard.
            }
        }

        text = HeadingPrefixRegex().Replace(text, "").Trim();

        var expectedIndex = text.IndexOf("Expected result:", StringComparison.OrdinalIgnoreCase);
        if (expectedIndex > 20) text = text[..expectedIndex].Trim();
        var instructionIndex = text.IndexOf("How to perform:", StringComparison.OrdinalIgnoreCase);
        if (instructionIndex > 20) text = text[..instructionIndex].Trim();

        text = MultiWhitespaceRegex().Replace(text, " ").Trim();
        return text;
    }

    private static bool ContainsGroundingAnchor(GuideStep step, string candidate)
    {
        var source = $"{step.Context?.ElementName} {step.Title} {step.Context?.HelpText}";
        var tokens = WordRegex().Matches(source)
            .Cast<Match>()
            .Select(match => match.Value)
            .Where(token => token.Length >= 4 && !StopWords.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();

        if (tokens.Length == 0) return true;
        return tokens.Any(token => candidate.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeInstructionInsteadOfPurpose(string candidate)
    {
        var trimmed = candidate.TrimStart();
        if (!(trimmed.StartsWith("Click ", StringComparison.OrdinalIgnoreCase) ||
              trimmed.StartsWith("Select ", StringComparison.OrdinalIgnoreCase) ||
              trimmed.StartsWith("Right-click ", StringComparison.OrdinalIgnoreCase)))
            return false;

        return !candidate.Contains(" so ", StringComparison.OrdinalIgnoreCase) &&
               !candidate.Contains(" which ", StringComparison.OrdinalIgnoreCase) &&
               !candidate.Contains(" to open ", StringComparison.OrdinalIgnoreCase) &&
               !candidate.Contains(" to switch ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksSpecificEnoughForVision(string candidate)
    {
        if (candidate.Contains("highlighted area", StringComparison.OrdinalIgnoreCase) &&
            !candidate.Contains("menu", StringComparison.OrdinalIgnoreCase) &&
            !candidate.Contains("button", StringComparison.OrdinalIgnoreCase) &&
            !candidate.Contains("tab", StringComparison.OrdinalIgnoreCase) &&
            !candidate.Contains("repository", StringComparison.OrdinalIgnoreCase) &&
            !candidate.Contains("branch", StringComparison.OrdinalIgnoreCase) &&
            !candidate.Contains("changes", StringComparison.OrdinalIgnoreCase) &&
            !candidate.Contains("history", StringComparison.OrdinalIgnoreCase))
            return false;

        return WordRegex().Matches(candidate)
            .Cast<Match>()
            .Select(match => match.Value)
            .Count(token => token.Length >= 5 && !StopWords.Contains(token)) >= 3;
    }

    [GeneratedRegex(@"^(?:what\s+this\s+does|purpose|description)\s*[:\-]\s*", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingPrefixRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhitespaceRegex();

    [GeneratedRegex(@"[A-Za-z0-9][A-Za-z0-9_./-]*")]
    private static partial Regex WordRegex();
}
