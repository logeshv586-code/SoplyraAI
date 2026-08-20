using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

public sealed class DescriptionService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public (string title, string description) DescribeFast(string action, UiContext c)
    {
        var type = string.IsNullOrWhiteSpace(c.LocalizedControlType)
            ? (string.IsNullOrWhiteSpace(c.ControlType) ? "item" : Humanize(c.ControlType))
            : c.LocalizedControlType.ToLowerInvariant();
        var name = CleanName(c.ElementName);
        var window = CleanName(c.WindowTitle);
        var target = !string.IsNullOrWhiteSpace(name) ? $"the “{name}” {type}" : $"the {type}";
        var title = $"{action} {(string.IsNullOrWhiteSpace(name) ? type : name)}";

        string description;
        if (action == "Right-click")
            description = $"Right-click {target} to open its context options.";
        else if (action == "Middle-click")
            description = $"Middle-click {target}.";
        else
            description = DescribeKnownAction(name, type, target);

        if (!string.IsNullOrWhiteSpace(c.HelpText) && c.HelpText.Length <= 140)
            description += $" {CleanName(c.HelpText)}";
        else if (!string.IsNullOrWhiteSpace(window) && !description.Contains(window, StringComparison.OrdinalIgnoreCase))
            description += $" This action is in {window}.";

        return (title, description);
    }

    private static string DescribeKnownAction(string name, string type, string target)
    {
        var n = name.ToLowerInvariant();
        if (ContainsAny(n, "save", "apply")) return $"Click {target} to save your changes.";
        if (ContainsAny(n, "submit", "send")) return $"Click {target} to submit the current information.";
        if (ContainsAny(n, "add", "new", "create")) return $"Click {target} to create or add a new item.";
        if (ContainsAny(n, "delete", "remove", "trash")) return $"Click {target} to remove the selected item.";
        if (ContainsAny(n, "search", "find")) return $"Click {target} to run the search.";
        if (ContainsAny(n, "upload", "attach", "browse")) return $"Click {target} to choose a file to upload or attach.";
        if (ContainsAny(n, "download", "export")) return $"Click {target} to download or export the result.";
        if (ContainsAny(n, "next", "continue")) return $"Click {target} to continue to the next step.";
        if (ContainsAny(n, "back", "previous")) return $"Click {target} to return to the previous step.";
        if (ContainsAny(n, "cancel")) return $"Click {target} to cancel this operation.";
        if (ContainsAny(n, "close", "done", "finish")) return $"Click {target} to finish or close the current task.";
        if (ContainsAny(n, "login", "log in", "sign in")) return $"Click {target} to sign in.";
        if (ContainsAny(n, "logout", "log out", "sign out")) return $"Click {target} to sign out.";
        if (ContainsAny(n, "edit", "modify")) return $"Click {target} to edit the selected item.";
        if (ContainsAny(n, "settings", "preferences")) return $"Click {target} to open settings.";
        if (ContainsAny(n, "copy")) return $"Click {target} to copy the selected content.";
        if (ContainsAny(n, "refresh", "reload")) return $"Click {target} to refresh the current view.";
        if (type.Contains("hyperlink", StringComparison.OrdinalIgnoreCase) || type == "link") return $"Open {target}.";
        if (type.Contains("tab", StringComparison.OrdinalIgnoreCase)) return $"Select {target} to switch to that section.";
        if (type.Contains("menu", StringComparison.OrdinalIgnoreCase)) return $"Choose {target} from the menu.";
        if (type.Contains("check", StringComparison.OrdinalIgnoreCase)) return $"Select {target} to change this option.";
        if (type.Contains("button", StringComparison.OrdinalIgnoreCase)) return $"Click {target} to continue this task.";
        return $"Click {target}.";
    }

    private static bool ContainsAny(string value, params string[] words) => words.Any(value.Contains);

    public async Task<string?> ImproveAsync(GuideStep step, AppSettings settings, CancellationToken ct = default)
    {
        if (!settings.UseLocalAi || string.IsNullOrWhiteSpace(settings.AiEndpoint)) return null;
        var endpoint = settings.AiEndpoint.TrimEnd('/') + "/chat/completions";
        var prompt = $"""
You rewrite one software procedure step. Return only one short imperative sentence, maximum 18 words.
Do not invent facts. Do not mention coordinates. Never include secrets or typed values.
Action: {step.Action}
Element: {step.Context.ElementName}
Control type: {step.Context.ControlType}
Help text: {step.Context.HelpText}
Window: {step.Context.WindowTitle}
Current: {step.Description}
""";

        var payload = new
        {
            model = settings.AiModel,
            temperature = 0.1,
            max_tokens = 60,
            messages = new[]
            {
                new { role = "system", content = "You write concise end-user software instructions." },
                new { role = "user", content = prompt }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(settings.AiApiKey))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AiApiKey);

        try
        {
            using var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim();
        }
        catch { return null; }
    }

    private static string Humanize(string value) => value.Replace("ControlType.", "").Replace("_", " ").ToLowerInvariant();
    private static string CleanName(string? value)
    {
        var text = (value ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
        return text.Length > 80 ? text[..80] + "…" : text;
    }
}
