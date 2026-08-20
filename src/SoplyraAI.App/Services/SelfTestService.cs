using SoplyraAI.Models;

namespace SoplyraAI.Services;

internal static class SelfTestService
{
    public static void Run()
    {
        var describer = new DescriptionService();
        var (title, description) = describer.DescribeFast("Click", new UiContext
        {
            ElementName = "Save",
            ControlType = "Button",
            LocalizedControlType = "button",
            WindowTitle = "Example"
        });
        if (!title.Contains("Save", StringComparison.OrdinalIgnoreCase) ||
            !description.Contains("save", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Fast description test failed.");

        var temp = Path.Combine(Path.GetTempPath(), "soplyraai-selftest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var session = new GuideSession { Title = "Self test", SessionFolder = temp };
            session.Steps.Add(new GuideStep
            {
                Number = 1,
                Title = title,
                Description = description,
                ScreenshotPath = Path.Combine(temp, "missing.png")
            });
            var exporter = new ExportService();
            var html = exporter.ExportHtml(session, Path.Combine(temp, "export"));
            var markdown = exporter.ExportMarkdown(session, Path.Combine(temp, "export"));
            if (!File.Exists(html) || !File.Exists(markdown))
                throw new InvalidOperationException("Export test failed.");
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }
    }
}
