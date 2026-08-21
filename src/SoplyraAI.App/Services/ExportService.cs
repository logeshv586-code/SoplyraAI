using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

public sealed class ExportService
{
    public string ExportHtml(GuideSession session, string folder)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "guide.html");
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>");
        sb.AppendLine("<meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\">");
        sb.AppendLine("<meta name='referrer' content='no-referrer'>");
        sb.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;background:#f6f7fb;color:#151722;margin:0}.wrap{max-width:980px;margin:0 auto;padding:48px 24px}.hero,.step{background:#fff;border:1px solid #e7e8f0;border-radius:20px;box-shadow:0 12px 38px rgba(45,42,90,.07)}.hero{padding:34px;margin-bottom:24px}.hero h1{margin:0 0 8px}.muted{color:#72768a}.step{padding:22px;margin:18px 0}.step img{display:block;max-width:100%;border-radius:14px;border:1px solid #ececf2;margin-top:16px}.num{display:inline-flex;width:32px;height:32px;border-radius:50%;align-items:center;justify-content:center;background:linear-gradient(135deg,#7657f8,#3485ef);color:white;font-weight:700;margin-right:10px}h2{font-size:19px}p{line-height:1.65;white-space:pre-line}</style></head><body><main class='wrap'>");
        sb.AppendLine($"<section class='hero'><h1>{WebUtility.HtmlEncode(PrivacySanitizer.Clean(session.Title, 200))}</h1><div class='muted'>{session.Steps.Count} steps · {WebUtility.HtmlEncode(session.DocumentationMode)} guide · Created {session.CreatedAt:dd MMM yyyy}</div></section>");

        foreach (var step in session.Steps)
        {
            var imageData = TryReadTrustedScreenshot(session, step, out var bytes) ? Convert.ToBase64String(bytes) : "";
            sb.AppendLine("<section class='step'>");
            sb.AppendLine($"<h2><span class='num'>{step.Number}</span>{WebUtility.HtmlEncode(PrivacySanitizer.Clean(step.Title, 240))}</h2>");
            sb.AppendLine($"<p>{WebUtility.HtmlEncode(PrivacySanitizer.Clean(step.Description, 4000))}</p>");
            if (imageData.Length > 0) sb.AppendLine($"<img src='data:image/png;base64,{imageData}' alt='Step {step.Number} screenshot'>");
            sb.AppendLine("</section>");
        }

        sb.AppendLine("</main></body></html>");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return path;
    }

    public string ExportMarkdown(GuideSession session, string folder)
    {
        Directory.CreateDirectory(folder);
        var imagesFolder = Path.Combine(folder, "images");
        Directory.CreateDirectory(imagesFolder);
        var path = Path.Combine(folder, "guide.md");
        var sb = new StringBuilder($"# {EscapeMarkdown(session.Title)}\n\n{session.Steps.Count} steps · {session.DocumentationMode} guide.\n\n");

        foreach (var step in session.Steps)
        {
            var name = $"step-{step.Number:000}.png";
            var copied = false;
            if (PathSecurity.TryGetTrustedPng(session.SessionFolder, step.ScreenshotPath, out var trusted, requireExists: true))
            {
                File.Copy(trusted, Path.Combine(imagesFolder, name), true);
                copied = true;
            }
            sb.AppendLine($"## {step.Number}. {EscapeMarkdown(step.Title)}\n");
            sb.AppendLine(EscapeMarkdown(step.Description));
            sb.AppendLine();
            if (copied) sb.AppendLine($"![Step {step.Number}](images/{name})\n");
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return path;
    }

    public string ExportDocx(GuideSession session, string folder)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "guide.docx");
        if (File.Exists(path)) File.Delete(path);

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"png\" ContentType=\"image/png\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
        WriteEntry(archive, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");

        var doc = new StringBuilder();
        var rels = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        doc.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><w:body>");
        AppendParagraph(doc, session.Title, 34, true);
        AppendParagraph(doc, $"{session.Steps.Count} steps · {session.DocumentationMode} guide · Created {session.CreatedAt:dd MMM yyyy}", 19, false);

        var imageIndex = 0;
        foreach (var step in session.Steps)
        {
            AppendParagraph(doc, $"Step {step.Number} · {step.Title}", 25, true);
            foreach (var line in PrivacySanitizer.Clean(step.Description, 4000).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                AppendParagraph(doc, line, 21, false);

            if (PathSecurity.TryGetTrustedPng(session.SessionFolder, step.ScreenshotPath, out var trusted, requireExists: true))
            {
                imageIndex++;
                var fileName = $"image{imageIndex}.png";
                var relId = $"rId{imageIndex}";
                var entry = archive.CreateEntry("word/media/" + fileName, CompressionLevel.Optimal);
                using (var input = File.OpenRead(trusted))
                using (var output = entry.Open()) input.CopyTo(output);
                rels.Append($"<Relationship Id=\"{relId}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"media/{fileName}\"/>");
                AppendImage(doc, relId, imageIndex);
            }
        }
        doc.Append("<w:sectPr><w:pgSz w:w=\"12240\" w:h=\"15840\"/><w:pgMar w:top=\"900\" w:right=\"900\" w:bottom=\"900\" w:left=\"900\"/></w:sectPr></w:body></w:document>");
        rels.Append("</Relationships>");
        WriteEntry(archive, "word/document.xml", doc.ToString());
        WriteEntry(archive, "word/_rels/document.xml.rels", rels.ToString());
        return path;
    }

    public async Task<string?> ExportPdfAsync(GuideSession session, string folder)
    {
        var html = ExportHtml(session, folder);
        var pdf = Path.Combine(folder, "guide.pdf");
        var browser = FindHeadlessBrowser();
        if (browser is null) return null;
        var profile = Path.Combine(Path.GetTempPath(), "soplyraai-pdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profile);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = browser,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(browser)!
            };
            foreach (var arg in new[] { "--headless", "--disable-gpu", "--disable-extensions", "--disable-background-networking", "--no-first-run", "--no-default-browser-check", "--print-to-pdf-no-header", $"--user-data-dir={profile}", $"--print-to-pdf={pdf}", new Uri(html).AbsoluteUri })
                psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi);
            if (process is null) return null;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(75));
            try { await process.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }
            return process.ExitCode == 0 && File.Exists(pdf) && new FileInfo(pdf).Length > 1000 ? pdf : null;
        }
        finally
        {
            try { Directory.Delete(profile, true); } catch { }
        }
    }

    public static string NewExportFolder(GuideSession session)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var cleanTitle = PrivacySanitizer.Clean(session.Title, 80);
        var safe = string.Concat(cleanTitle.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Trim();
        if (string.IsNullOrWhiteSpace(safe)) safe = "Guide";
        return Path.Combine(desktop, "SoplyraAI Exports", $"{safe}-{DateTime.Now:yyyyMMdd-HHmmss}");
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void AppendParagraph(StringBuilder sb, string? text, int halfPoints, bool bold)
    {
        var safe = Xml(PrivacySanitizer.Clean(text, 4000));
        sb.Append("<w:p><w:r><w:rPr>");
        if (bold) sb.Append("<w:b/>");
        sb.Append($"<w:sz w:val=\"{halfPoints}\"/></w:rPr><w:t xml:space=\"preserve\">{safe}</w:t></w:r></w:p>");
    }

    private static void AppendImage(StringBuilder sb, string relId, int id)
    {
        const long cx = 5486400;
        const long cy = 3086100;
        sb.Append($"<w:p><w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:docPr id=\"{id}\" name=\"Step screenshot {id}\"/><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic><pic:nvPicPr><pic:cNvPr id=\"{id}\" name=\"Screenshot {id}\"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed=\"{relId}\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>");
    }

    private static string Xml(string value) => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");

    private static bool TryReadTrustedScreenshot(GuideSession session, GuideStep step, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (!PathSecurity.TryGetTrustedPng(session.SessionFolder, step.ScreenshotPath, out var trusted, requireExists: true)) return false;
        try { bytes = File.ReadAllBytes(trusted); return true; }
        catch { bytes = Array.Empty<byte>(); return false; }
    }

    private static string EscapeMarkdown(string? value)
    {
        var text = PrivacySanitizer.Clean(value, 4000);
        var sb = new StringBuilder(text.Length + 16);
        foreach (var ch in text) { if ("\\`*_{}[]<>()#+-.!|".Contains(ch)) sb.Append('\\'); sb.Append(ch); }
        return sb.ToString();
    }

    private static string? FindHeadlessBrowser()
    {
        string[] paths =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe")
        };
        return paths.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
    }
}
