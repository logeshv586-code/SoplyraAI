namespace SoplyraAI.Models;

public sealed class AppSettings
{
    public bool UseLocalAi { get; set; } = true;
    public string AiEndpoint { get; set; } = "http://127.0.0.1:11434/v1";
    public string AiModel { get; set; } = "qwen2.5:0.5b";
    public string AiApiKey { get; set; } = "ollama";
    public string ScreenshotMode { get; set; } = "ActiveWindow";
    public int CaptureDelayMs { get; set; } = 180;
}
