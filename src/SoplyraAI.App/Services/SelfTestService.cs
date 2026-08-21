using System.IO.Compression;
using System.Text;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

internal static class SelfTestService
{
    public static void Run()
    {
        TestDescriptionModes();
        TestProviderCatalog();
        TestAiEndpointPolicy();
        TestSettingsSecretProtection();
        TestExportHardening();
        TestSessionDelete();
    }

    private static void TestDescriptionModes()
    {
        var describer = new DescriptionService();
        var context = new UiContext { ElementName = "Save", ControlType = "Button", LocalizedControlType = "button", WindowTitle = "Example" };
        var quick = describer.DescribeFast("Click", context, "Quick");
        var detailed = describer.DescribeFast("Click", context, "Detailed");
        if (!quick.title.Contains("Save", StringComparison.OrdinalIgnoreCase) || !quick.description.Contains("save", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Quick description test failed.");
        if (detailed.description.Length <= quick.description.Length || !detailed.description.Contains("selected control", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Detailed description test failed.");
    }

    private static void TestProviderCatalog()
    {
        foreach (var id in new[] { "Ollama", "OpenAI", "DeepSeek", "NVIDIA", "Gemini", "Anthropic" })
        {
            var provider = AiProviderCatalog.Get(id);
            if (provider.Id != id || string.IsNullOrWhiteSpace(provider.Endpoint) || string.IsNullOrWhiteSpace(provider.DefaultModel))
                throw new InvalidOperationException($"Provider catalog test failed for {id}.");
        }
        if (!AiProviderCatalog.IsVisionModel("Ollama", "qwen2.5vl:3b"))
            throw new InvalidOperationException("Local vision-model detection failed.");
    }

    private static void TestAiEndpointPolicy()
    {
        if (!AiEndpointPolicy.TryValidate("http://127.0.0.1:11434/v1", false, out _, out _)) throw new InvalidOperationException("Loopback AI endpoint policy test failed.");
        if (AiEndpointPolicy.TryValidate("http://example.com/v1", true, out _, out _)) throw new InvalidOperationException("Remote HTTP endpoint should be blocked.");
        if (AiEndpointPolicy.TryValidate("https://example.com/v1", false, out _, out _)) throw new InvalidOperationException("Remote endpoint should require explicit opt-in.");
        if (!AiEndpointPolicy.TryValidate("https://example.com/v1", true, out _, out _)) throw new InvalidOperationException("Opted-in remote HTTPS endpoint should be accepted.");
    }

    private static void TestSettingsSecretProtection()
    {
        var temp = Path.Combine(Path.GetTempPath(), "soplyraai-settings-selftest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            const string secret = "self-test-api-key-123456";
            var store = new SettingsStore(temp);
            var settings = new AppSettings { AiProvider = "OpenAI", AiApiKey = secret, HasCompletedAiSetup = true };
            store.Save(settings);
            var raw = File.ReadAllText(Path.Combine(temp, "settings.json"), Encoding.UTF8);
            if (raw.Contains(secret, StringComparison.Ordinal)) throw new InvalidOperationException("API key was stored in plaintext.");
            if (store.Load().AiApiKey != secret) throw new InvalidOperationException("Protected API key round-trip failed.");
        }
        finally { try { Directory.Delete(temp, true); } catch { } }
    }

    private static void TestExportHardening()
    {
        var temp = Path.Combine(Path.GetTempPath(), "soplyraai-export-selftest-" + Guid.NewGuid().ToString("N"));
        var sessionFolder = Path.Combine(temp, "session");
        var images = Path.Combine(sessionFolder, "images");
        Directory.CreateDirectory(images);
        try
        {
            var outside = Path.Combine(temp, "outside.png");
            File.WriteAllBytes(outside, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            if (PathSecurity.TryGetTrustedPng(sessionFolder, outside, out _, requireExists: true)) throw new InvalidOperationException("Path boundary test failed.");

            var session = new GuideSession { Title = "<script>alert(1)</script>", SessionFolder = sessionFolder, DocumentationMode = "Detailed" };
            session.Steps.Add(new GuideStep { Number = 1, Title = "Save [link]", Description = "[click](javascript:alert(1))", ScreenshotPath = outside });
            var exportFolder = Path.Combine(temp, "export");
            var exporter = new ExportService();
            var html = exporter.ExportHtml(session, exportFolder);
            var markdown = exporter.ExportMarkdown(session, exportFolder);
            var docx = exporter.ExportDocx(session, exportFolder);

            var htmlText = File.ReadAllText(html, Encoding.UTF8);
            var markdownText = File.ReadAllText(markdown, Encoding.UTF8);
            if (htmlText.Contains("<script>", StringComparison.OrdinalIgnoreCase) || !htmlText.Contains("Content-Security-Policy", StringComparison.Ordinal)) throw new InvalidOperationException("HTML export hardening test failed.");
            if (markdownText.Contains("[click](", StringComparison.Ordinal)) throw new InvalidOperationException("Markdown escaping test failed.");
            using var zip = ZipFile.OpenRead(docx);
            if (zip.GetEntry("word/document.xml") is null) throw new InvalidOperationException("DOCX export package test failed.");
            if (Directory.EnumerateFiles(Path.Combine(exportFolder, "images"), "*", SearchOption.TopDirectoryOnly).Any()) throw new InvalidOperationException("Untrusted screenshot path was exported.");
        }
        finally { try { Directory.Delete(temp, true); } catch { } }
    }

    private static void TestSessionDelete()
    {
        var temp = Path.Combine(Path.GetTempPath(), "soplyraai-session-selftest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SessionStore(temp);
            var session = store.Create("Delete me");
            if (!Directory.Exists(session.SessionFolder)) throw new InvalidOperationException("Session creation test failed.");
            if (!store.Delete(session.Id) || Directory.Exists(session.SessionFolder)) throw new InvalidOperationException("Recent-guide deletion test failed.");
        }
        finally { try { Directory.Delete(temp, true); } catch { } }
    }
}
