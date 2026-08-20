using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, MaxDepth = 16 };
    private readonly string _path;

    public SettingsStore(string? rootFolder = null)
    {
        var root = rootFolder ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoplyraAI");
        _path = Path.Combine(root, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();

            var info = new FileInfo(_path);
            if (info.Length > 256 * 1024) return new AppSettings();

            var json = File.ReadAllText(_path, Encoding.UTF8);
            var stored = JsonSerializer.Deserialize<PersistedSettings>(json, JsonOptions);
            if (stored is null) return new AppSettings();

            var settings = new AppSettings
            {
                UseLocalAi = stored.UseLocalAi,
                AllowRemoteAi = stored.AllowRemoteAi,
                AiEndpoint = PrivacySanitizer.Clean(stored.AiEndpoint, 2048),
                AiModel = PrivacySanitizer.Clean(stored.AiModel, 120),
                ScreenshotMode = string.Equals(stored.ScreenshotMode, "FullDesktop", StringComparison.OrdinalIgnoreCase)
                    ? "FullDesktop"
                    : "ActiveWindow",
                CaptureDelayMs = Math.Clamp(stored.CaptureDelayMs, 0, 1000),
                AiApiKey = Unprotect(stored.ProtectedAiApiKey)
            };

            if (string.IsNullOrEmpty(settings.AiApiKey))
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("AiApiKey", out var legacy) &&
                    legacy.ValueKind == JsonValueKind.String)
                {
                    settings.AiApiKey = legacy.GetString() ?? "";
                    Save(settings);
                }
            }

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        var stored = new PersistedSettings
        {
            UseLocalAi = settings.UseLocalAi,
            AllowRemoteAi = settings.AllowRemoteAi,
            AiEndpoint = PrivacySanitizer.Clean(settings.AiEndpoint, 2048),
            AiModel = PrivacySanitizer.Clean(settings.AiModel, 120),
            ProtectedAiApiKey = Protect(settings.AiApiKey),
            ScreenshotMode = settings.ScreenshotMode.Equals("FullDesktop", StringComparison.OrdinalIgnoreCase)
                ? "FullDesktop"
                : "ActiveWindow",
            CaptureDelayMs = Math.Clamp(settings.CaptureDelayMs, 0, 1000)
        };

        var json = JsonSerializer.Serialize(stored, JsonOptions);
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, json, new UTF8Encoding(false));
        File.Move(temp, _path, true);
    }

    private static string Protect(string? secret)
    {
        if (string.IsNullOrEmpty(secret)) return "";
        var data = Encoding.UTF8.GetBytes(secret);
        try
        {
            var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }
        catch
        {
            return "";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(data);
        }
    }

    private static string Unprotect(string? protectedSecret)
    {
        if (string.IsNullOrWhiteSpace(protectedSecret)) return "";
        byte[]? plain = null;
        try
        {
            var encrypted = Convert.FromBase64String(protectedSecret);
            plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return "";
        }
        finally
        {
            if (plain is not null) CryptographicOperations.ZeroMemory(plain);
        }
    }

    private sealed class PersistedSettings
    {
        public PersistedSettings() { }

        public bool UseLocalAi { get; set; } = true;
        public bool AllowRemoteAi { get; set; }
        public string AiEndpoint { get; set; } = "http://127.0.0.1:11434/v1";
        public string AiModel { get; set; } = "qwen2.5:0.5b";
        public string ProtectedAiApiKey { get; set; } = "";
        public string ScreenshotMode { get; set; } = "ActiveWindow";
        public int CaptureDelayMs { get; set; } = 180;
    }
}
