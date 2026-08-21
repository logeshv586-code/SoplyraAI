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
        TestStructuredExportContent();
        TestPdfCompletenessValidator();
        TestSessionRenameAndDelete();
    }

    private static void TestDescriptionModes()
    {
        var describer = new DescriptionService();
        var context = new UiContext
        {
            ElementName = "Save",
            ControlType = "Button",
            LocalizedControlType = "button",
            WindowTitle = "Example"
        };

        var quick = describer.DescribeFast("Click", context, "Quick");
        var detailed = describer.DescribeFast("Click", context, "Detailed");
        if (!quick.title.Contains("Save", StringComparison.OrdinalIgnoreCase) ||
            !quick.description.Contains("save", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Quick description test failed.");

        if (detailed.description.Length <= quick.description.Length ||
            !detailed.description.Contains("selected control", StringComparison.OrdinalIgnoreCase))
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
        if (!AiEndpointPolicy.TryValidate("http://127.0.0.1:11434/v1", false, out _, out _))
            throw new InvalidOperationException("Loopback AI endpoint policy test failed.");
        if (AiEndpointPolicy.TryValidate("http://example.com/v1", true, out _, out _))
            throw new InvalidOperationException("Remote HTTP endpoint should be blocked.");
        if (AiEndpointPolicy.TryValidate("https://example.com/v1", false, out _, out _))
            throw new InvalidOperationException("Remote endpoint should require explicit opt-in.");
        if (!AiEndpointPolicy.TryValidate("https://example.com/v1", true, out _, out _))
            throw new InvalidOperationException("Opted-in remote HTTPS endpoint should be accepted.");
    }

    private static void TestSettingsSecretProtection()
    {
        var temp = Path.Combine(Path.GetTempPath(), "soplyraai-settings-selftest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            const string secret = "self-test-api-key-123456";
            var store = new SettingsStore(temp);
            var settings = new AppSettings
            {
                AiProvider = "OpenAI",
                AiApiKey = secret,
                HasCompletedAiSetup = true
            };
            store.Save(settings);

            var raw = File.ReadAllText(Path.Combine(temp, "settings.json"), Encoding.UTF8);
            if (raw.Contains(secret, StringComparison.Ordinal))
                throw new InvalidOperationException("API key was stored in plaintext.");
            if (store.Load().AiApiKey != secret)
                throw new InvalidOperationException("Protected API key round-trip failed.");
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    private static void TestStructuredExportContent()
    {
        var temp = Path.Combine(Path.GetTempPath(), "soplyraai-export-selftest-" + Guid.NewGuid().ToString("N"));
        var sessionFolder = Path.Combine(temp, "session");
        var imagesFolder = Path.Combine(sessionFolder, "images");
        Directory.CreateDirectory(imagesFolder);

        try
        {
            var imagePath = Path.Combine(imagesFolder, "step-001.png");
            var onePixelPng = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
            File.WriteAllBytes(imagePath, onePixelPng);

            var outside = Path.Combine(temp, "outside.png");
            File.WriteAllBytes(outside, onePixelPng);
            if (PathSecurity.TryGetTrustedPng(sessionFolder, outside, out _, requireExists: true))
                throw new InvalidOperationException("Path boundary test failed.");

            var session = new GuideSession
            {
                Title = "Customer request workflow",
                SessionFolder = sessionFolder,
                DocumentationMode = "Detailed"
            };
            session.Steps.Add(new GuideStep
            {
                Number = 1,
                Action = "Click",
                Title = "Click Egonex-AI/Understand-Anything: Graphs that teach > graphs that impress. Turn any code into an interactive knowledge graph you can explore, search, and ask questions",
                Description = "Click Save to store the customer request before continuing.",
                ScreenshotPath = imagePath,
                Context = new UiContext
                {
                    ElementName = "Save",
                    ControlType = "Button",
                    LocalizedControlType = "button",
                    ProcessName = "ExampleApp",
                    WindowTitle = "Customer Request"
                }
            });
            session.Steps.Add(new GuideStep
            {
                Number = 2,
                Action = "Click",
                Title = "",
                Description = "",
                ScreenshotPath = "",
                Context = new UiContext
                {
                    ElementName = "Submit",
                    ControlType = "Button",
                    LocalizedControlType = "button",
                    ProcessName = "ExampleApp",
                    WindowTitle = "Customer Request"
                }
            });

            var exportFolder = Path.Combine(temp, "export");
            var exporter = new ExportService();
            var html = exporter.ExportHtml(session, exportFolder);
            var markdown = exporter.ExportMarkdown(session, exportFolder);
            var docx = exporter.ExportDocx(session, exportFolder);
            var pdfExporter = new ReliablePdfExportService(exporter);
            var pdf = pdfExporter.ExportAsync(session, exportFolder).GetAwaiter().GetResult();

            var htmlText = File.ReadAllText(html, Encoding.UTF8);
            if (!htmlText.Contains("Customer request workflow", StringComparison.Ordinal) ||
                !htmlText.Contains("How to perform", StringComparison.Ordinal) ||
                !htmlText.Contains("What this does", StringComparison.Ordinal) ||
                !htmlText.Contains("Expected result", StringComparison.Ordinal) ||
                !htmlText.Contains("data:image/png;base64,", StringComparison.Ordinal) ||
                !htmlText.Contains("Click Submit", StringComparison.Ordinal))
                throw new InvalidOperationException("HTML structured-content export test failed.");

            if (htmlText.IndexOf("data:image/png;base64,", StringComparison.Ordinal) >
                htmlText.IndexOf("How to perform", StringComparison.Ordinal))
                throw new InvalidOperationException("HTML screenshot must appear before the explanation.");

            var markdownText = File.ReadAllText(markdown, Encoding.UTF8);
            if (!markdownText.Contains("![Captured screen for Step 1]", StringComparison.Ordinal) ||
                !markdownText.Contains("### How to perform", StringComparison.Ordinal) ||
                !markdownText.Contains("### What this does", StringComparison.Ordinal) ||
                !markdownText.Contains("### Expected result", StringComparison.Ordinal) ||
                !markdownText.Contains("Step 2: Click Submit", StringComparison.Ordinal))
                throw new InvalidOperationException("Markdown structured-content export test failed.");

            if (markdownText.IndexOf("![Captured screen for Step 1]", StringComparison.Ordinal) >
                markdownText.IndexOf("### How to perform", StringComparison.Ordinal))
                throw new InvalidOperationException("Markdown screenshot must appear before the explanation.");

            using (var zip = ZipFile.OpenRead(docx))
            {
                var documentEntry = zip.GetEntry("word/document.xml") ??
                    throw new InvalidOperationException("DOCX export package is missing document.xml.");
                using var reader = new StreamReader(documentEntry.Open(), Encoding.UTF8);
                var documentXml = reader.ReadToEnd();

                if (!documentXml.Contains("Customer request workflow", StringComparison.Ordinal) ||
                    !documentXml.Contains("Step 1: Click Egonex-AI/Understand-Anything", StringComparison.Ordinal) ||
                    !documentXml.Contains("Step 2: Click Submit", StringComparison.Ordinal) ||
                    !documentXml.Contains("How to perform", StringComparison.Ordinal) ||
                    !documentXml.Contains("What this does", StringComparison.Ordinal) ||
                    !documentXml.Contains("Expected result", StringComparison.Ordinal))
                    throw new InvalidOperationException("DOCX visible-content export test failed.");

                if (!zip.Entries.Any(entry => entry.FullName.StartsWith("word/media/", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("DOCX screenshot embedding test failed.");
            }

            if (string.IsNullOrWhiteSpace(pdf) || !File.Exists(pdf) || !ReliablePdfExportService.IsCompletePdf(pdf))
                throw new InvalidOperationException("Native PDF export test failed.");

            var pdfBytes = File.ReadAllBytes(pdf);
            var pdfAscii = Encoding.ASCII.GetString(pdfBytes);
            if (!pdfAscii.Contains("/Count 2", StringComparison.Ordinal) ||
                pdfAscii.Contains("/Count 3", StringComparison.Ordinal) ||
                pdfAscii.CountOccurrences("/Subtype /Image") < 2)
                throw new InvalidOperationException("Native PDF must contain exactly one page per recorded step and no separate overview page.");
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    private static void TestPdfCompletenessValidator()
    {
        var temp = Path.Combine(Path.GetTempPath(), "soplyraai-pdf-validator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var complete = Path.Combine(temp, "complete.pdf");
            var incomplete = Path.Combine(temp, "incomplete.pdf");

            var completeBytes = Encoding.ASCII.GetBytes("%PDF-1.7\n" + new string(' ', 5000) + "\n%%EOF\n");
            File.WriteAllBytes(complete, completeBytes);
            File.WriteAllBytes(incomplete, Encoding.ASCII.GetBytes("%PDF-1.7\n" + new string(' ', 5000)));

            if (!ReliablePdfExportService.IsCompletePdf(complete))
                throw new InvalidOperationException("Complete PDF validation test failed.");
            if (ReliablePdfExportService.IsCompletePdf(incomplete))
                throw new InvalidOperationException("Incomplete PDF should not pass validation.");
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    private static void TestSessionRenameAndDelete()
    {
        var temp = Path.Combine(Path.GetTempPath(), "soplyraai-session-selftest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SessionStore(temp);
            var session = store.Create("Untitled guide");
            if (!Directory.Exists(session.SessionFolder))
                throw new InvalidOperationException("Session creation test failed.");

            session.Title = "Customer Onboarding SOP";
            store.Save(session);
            var reloaded = store.LoadAll().FirstOrDefault(item => item.Id == session.Id);
            if (reloaded?.Title != "Customer Onboarding SOP")
                throw new InvalidOperationException("Saved workflow rename persistence test failed.");

            if (!store.Delete(session.Id) || Directory.Exists(session.SessionFolder))
                throw new InvalidOperationException("Recent-guide deletion test failed.");
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }
    }
}
