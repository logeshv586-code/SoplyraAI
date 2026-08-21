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
        TestStepRemove();
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
                Description = "Click Save to store the customer request before continuing. Review the resulting screen and confirm that the information remains visible before continuing to the next recorded action.",
                ScreenshotPath = imagePath,
                Context = new UiContext
                {
                    ElementName = "Save",
                    ControlType = "Button",
                    LocalizedControlType = "button",
                    ProcessName = "ExampleApp",
                    WindowTitle = "curl -X POST http://192.168.1.106:8000/api/very/long/path with request parameters and a long captured window title that must wrap without being hidden"
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

    private static void TestStepRemove()
    {
        var temp = Path.Combine(Path.GetTempPath(), "soplyraai-step-remove-selftest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SessionStore(temp);
            var session = store.Create("Step remove test");
            var images = Path.Combine(session.SessionFolder, "images");
            var firstImage = Path.Combine(images, "step-001.png");
            File.WriteAllBytes(firstImage, Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));

            var first = new GuideStep { Number = 1, Title = "First", ScreenshotPath = firstImage };
            var second = new GuideStep { Number = 2, Title = "Second" };
            var third = new GuideStep { Number = 3, Title = "Third" };
            session.Steps.Add(first);
            session.Steps.Add(second);
            session.Steps.Add(third);

            var liveCollection = session.Steps;
            store.Save(session);
            if (!ReferenceEquals(liveCollection, session.Steps))
                throw new InvalidOperationException("Saving a guide replaced its live step collection and would break WPF Remove bindings.");

            if (!store.DeleteStep(session, second.Id))
                throw new InvalidOperationException("Per-step removal returned false for an existing captured step.");
            if (!ReferenceEquals(liveCollection, session.Steps))
                throw new InvalidOperationException("Per-step removal replaced the live step collection.");
            if (session.Steps.Count != 2 || session.Steps.Any(step => step.Id == second.Id))
                throw new InvalidOperationException("Per-step removal did not remove the requested step.");
            if (session.Steps[0].Number != 1 || session.Steps[1].Number != 2)
                throw new InvalidOperationException("Remaining captured steps were not renumbered after removal.");

            var reloaded = store.LoadAll().FirstOrDefault(item => item.Id == session.Id);
            if (reloaded is null || reloaded.Steps.Count != 2 || reloaded.Steps.Any(step => step.Id == second.Id))
                throw new InvalidOperationException("Per-step removal was not persisted to session storage.");

            if (!store.DeleteStep(session, first.Id))
                throw new InvalidOperationException("Screenshot-backed step removal failed.");
            if (File.Exists(firstImage))
                throw new InvalidOperationException("Removed step screenshot was not cleaned up.");
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
            var image = Path.Combine(session.SessionFolder, "images", "locked-step.png");
            File.WriteAllBytes(image, Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
            session.Steps.Add(new GuideStep { Number = 1, Title = "Locked screenshot step", ScreenshotPath = image });
            store.Save(session);

            var reloaded = store.LoadAll().FirstOrDefault(item => item.Id == session.Id);
            if (reloaded?.Title != "Customer Onboarding SOP")
                throw new InvalidOperationException("Saved workflow rename persistence test failed.");

            // Simulate the exact desktop problem: an image decoder/antivirus keeps the screenshot
            // open while the user presses Delete. Logical deletion must still succeed immediately.
            using (var locked = new FileStream(image, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                if (!store.Delete(session.Id))
                    throw new InvalidOperationException("Saved workflow deletion failed while a screenshot file was locked.");

                var sessionJson = Path.Combine(session.SessionFolder, "session.json");
                if (File.Exists(sessionJson))
                    throw new InvalidOperationException("Deleted workflow session.json still exists.");
                if (store.LoadAll().Any(item => item.Id == session.Id))
                    throw new InvalidOperationException("Deleted workflow reappeared while its screenshot directory was awaiting cleanup.");
            }

            // Once the lock is released, LoadAll performs best-effort orphan cleanup.
            _ = store.LoadAll();
            if (store.LoadAll().Any(item => item.Id == session.Id))
                throw new InvalidOperationException("Deleted workflow returned after cleanup.");
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }
    }
}
