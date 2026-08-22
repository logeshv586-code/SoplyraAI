using System.Text.Json.Serialization;

namespace SoplyraAI.Models;

public sealed class AppSettings
{
    public bool EnableAi { get; set; } = true;
    public bool UseLocalAi { get; set; } = true;
    public bool AllowRemoteAi { get; set; }
    public bool SendScreenshotsToAi { get; set; }
    public bool HasCompletedAiSetup { get; set; }
    public string AiProvider { get; set; } = "Ollama";
    public string AiEndpoint { get; set; } = "http://127.0.0.1:11434/v1";
    public string AiModel { get; set; } = "qwen3:4b";
    public string DocumentationMode { get; set; } = "Quick";
    public string DefaultExportFormat { get; set; } = "PDF";

    [JsonIgnore]
    public string AiApiKey { get; set; } = "";

    public string ScreenshotMode { get; set; } = "ActiveWindow";
    public int CaptureDelayMs { get; set; } = 180;
}
