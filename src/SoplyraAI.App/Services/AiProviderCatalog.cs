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
            "Private by default. qwen3:4b is a strong documentation model; qwen2.5vl:3b and gemma3:4b add vision."),
        new AiProviderOption(
            "OpenAI", "OpenAI", "https://api.openai.com/v1", "gpt-4.1-mini", true,
            new[] { "gpt-4.1-mini", "gpt-4.1", "gpt-4o-mini" },
            "Strong instruction quality and image understanding."),
        new AiProviderOption(
            "DeepSeek", "DeepSeek", "https://api.deepseek.com", "deepseek-v4-flash", false,
            new[] { "deepseek-v4-flash", "deepseek-v4-pro" },
            "Excellent text reasoning and value. Use another provider/local VLM when screenshot vision is required."),
        new AiProviderOption(
            "NVIDIA", "NVIDIA NIM", "https://integrate.api.nvidia.com/v1", "meta/llama-3.2-11b-vision-instruct", true,
            new[] { "meta/llama-3.2-11b-vision-instruct", "google/gemma-4-31b-it", "openai/gpt-oss-20b" },
            "OpenAI-compatible hosted NIM endpoints with text and vision model choices."),
        new AiProviderOption(
            "Gemini", "Google Gemini", "https://generativelanguage.googleapis.com/v1beta", "gemini-3.5-flash", true,
            new[] { "gemini-3.5-flash", "gemini-2.5-flash", "gemini-2.5-pro" },
            "Fast multimodal models suited to screenshot understanding."),
        new AiProviderOption(
            "Anthropic", "Anthropic Claude", "https://api.anthropic.com", "claude-sonnet-4-20250514", true,
            new[] { "claude-sonnet-4-20250514", "claude-opus-4-20250514" },
            "Strong structured writing and vision for detailed SOP generation.")
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
            return m.Contains("vision") || m.Contains("omni") || m.Contains("gemma-4");
        return true;
    }
}
