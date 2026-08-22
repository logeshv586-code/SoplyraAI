namespace SoplyraAI.Services;

public sealed record AiProviderOption(
    string Id,
    string DisplayName,
    string Endpoint,
    string DefaultModel,
    bool SupportsVision,
    IReadOnlyList<string> Models,
    string Note);

public static class AiProviderCatalog
{
    public static readonly IReadOnlyList<string> LocalModels = new[]
    {
        "qwen3:4b",
        "qwen2.5vl:3b",
        "deepseek-r1:7b",
        "gemma3:4b"
    };

    private static readonly IReadOnlyList<AiProviderOption> Providers = new[]
    {
        new AiProviderOption(
            "Ollama", "Ollama · Local", "http://127.0.0.1:11434/v1", "qwen3:4b", true,
            LocalModels,
            "Private local inference. qwen3:4b is recommended for text documentation; qwen2.5vl:3b and gemma3:4b add screenshot vision."),
        new AiProviderOption(
            "OpenAI", "OpenAI", "https://api.openai.com/v1", "gpt-5.6-luna", true,
            new[] { "gpt-5.6-luna", "gpt-5.6-terra", "gpt-5.6-sol" },
            "Uses OpenAI's Responses API. The model field is editable, so you can enter another model ID available to your OpenAI project."),
        new AiProviderOption(
            "DeepSeek", "DeepSeek", "https://api.deepseek.com", "deepseek-v4-flash", false,
            new[] { "deepseek-v4-flash", "deepseek-v4-pro" },
            "Uses DeepSeek's OpenAI-compatible /chat/completions API in non-thinking mode for concise documentation."),
        new AiProviderOption(
            "NVIDIA", "NVIDIA NIM", "https://integrate.api.nvidia.com/v1", "google/gemma-4-31b-it", true,
            new[] { "google/gemma-4-31b-it", "openai/gpt-oss-20b", "meta/llama-3.3-70b-instruct" },
            "Uses NVIDIA's OpenAI-compatible /v1/chat/completions endpoint. Gemma 4 supports image input; the model field also accepts other compatible NIM model IDs."),
        new AiProviderOption(
            "Gemini", "Google Gemini", "https://generativelanguage.googleapis.com/v1beta", "gemini-3.7-flash", true,
            new[] { "gemini-3.7-flash", "gemini-3.6-flash", "gemini-3.5-flash-lite" },
            "Uses Gemini generateContent with current Gemini 3.x request rules. The model field is editable for other supported Gemini model IDs."),
        new AiProviderOption(
            "Anthropic", "Anthropic Claude", "https://api.anthropic.com", "claude-sonnet-5", true,
            new[] { "claude-sonnet-5", "claude-opus-5", "claude-haiku-4-5" },
            "Uses Anthropic's Messages API. Current Claude models accept text and image input; you can also enter another model ID available to your account.")
    };

    public static IReadOnlyList<AiProviderOption> All => Providers;
    public static IReadOnlyList<AiProviderOption> Cloud => Providers.Where(x => x.Id != "Ollama").ToArray();

    public static AiProviderOption Get(string? id) =>
        Providers.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? Providers[0];

    public static bool IsLocal(string? id) =>
        string.Equals(id, "Ollama", StringComparison.OrdinalIgnoreCase);

    public static bool IsVisionModel(string? provider, string? model)
    {
        var p = Get(provider);
        if (!p.SupportsVision) return false;
        var m = (model ?? "").ToLowerInvariant();
        if (p.Id == "Ollama")
            return m.Contains("vl") || m.Contains("vision") || m.StartsWith("gemma3", StringComparison.Ordinal);
        if (p.Id == "NVIDIA")
            return m.Contains("vision") || m.Contains("omni") || m.Contains("gemma-4") || m.Contains("gemma4");
        return true;
    }
}
