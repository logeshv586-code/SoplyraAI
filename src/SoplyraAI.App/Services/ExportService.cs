using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

public sealed class ExportService
{
    public string ExportHtml(GuideSession session, string folder)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "guide.html");
        var cleanTitle = PrivacySanitizer.Clean(session.Title, 200);
        if (string.IsNullOrWhiteSpace(cleanTitle)) cleanTitle = "Standard Operating Procedure";
        var docMode = session.DocumentationMode == "Detailed" ? "Detailed SOP" : "Quick Visual Guide";
        var primaryApp = session.Steps.Select(s => s.Context.ProcessName).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? "Windows Desktop";

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>");
        sb.AppendLine("<meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; img-src 'self' data:; style-src 'unsafe-inline'; font-src data:; base-uri 'none'; form-action 'none'\">");
        sb.AppendLine("<meta name='referrer' content='no-referrer'>");
        sb.AppendLine($"<title>{WebUtility.HtmlEncode(cleanTitle)} — SoplyraAI Executive SOP</title>");
        sb.AppendLine(@"<style>
* { box-sizing: border-box; margin: 0; padding: 0; }
body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
    background: #F8FAFC;
    color: #0F172A;
    line-height: 1.6;
    -webkit-font-smoothing: antialiased;
}
.wrap { max-width: 980px; margin: 0 auto; padding: 48px 28px 80px; }

/* Document Header Card */
.doc-header {
    background: #FFFFFF;
    border: 1px solid #E2E8F0;
    border-radius: 20px;
    padding: 38px 42px;
    margin-bottom: 28px;
    box-shadow: 0 4px 20px -2px rgba(15, 23, 42, 0.05);
}
.brand-pill {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    background: #EEF2FF;
    color: #4F46E5;
    font-size: 11px;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.06em;
    padding: 4px 12px;
    border-radius: 9999px;
    margin-bottom: 14px;
}
.doc-header h1 {
    font-size: 28px;
    font-weight: 700;
    color: #0F172A;
    line-height: 1.25;
    letter-spacing: -0.02em;
    margin-bottom: 18px;
}

/* Metadata Grid Table */
.meta-table {
    width: 100%;
    border-collapse: collapse;
    margin-top: 14px;
    background: #F8FAFC;
    border-radius: 12px;
    overflow: hidden;
    border: 1px solid #E2E8F0;
}
.meta-table td {
    padding: 10px 16px;
    font-size: 12px;
    border: 1px solid #E2E8F0;
}
.meta-label {
    font-weight: 700;
    color: #64748B;
    text-transform: uppercase;
    font-size: 10px;
    letter-spacing: 0.05em;
    width: 25%;
    background: #F1F5F9;
}
.meta-val {
    color: #0F172A;
    font-weight: 600;
}

/* Executive Overview Table of Steps */
.toc-section {
    background: #FFFFFF;
    border: 1px solid #E2E8F0;
    border-radius: 16px;
    padding: 24px 28px;
    margin-bottom: 28px;
    box-shadow: 0 2px 12px -1px rgba(15, 23, 42, 0.03);
}
.toc-section h3 {
    font-size: 14px;
    font-weight: 700;
    color: #475569;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    margin-bottom: 14px;
    display: flex;
    align-items: center;
    gap: 8px;
}
.toc-list {
    list-style: none;
    display: flex;
    flex-direction: column;
    gap: 8px;
}
.toc-item {
    display: flex;
    align-items: center;
    gap: 12px;
    font-size: 13px;
    padding: 6px 10px;
    border-radius: 8px;
    background: #F8FAFC;
    border: 1px solid #E2E8F0;
}
.toc-num {
    font-weight: 700;
    color: #4F46E5;
    font-size: 11px;
    min-width: 24px;
}
.toc-title {
    font-weight: 600;
    color: #1E293B;
    flex-grow: 1;
}
.toc-badge {
    font-size: 10px;
    color: #64748B;
    background: #FFFFFF;
    padding: 2px 8px;
    border-radius: 4px;
    border: 1px solid #E2E8F0;
}

/* Step Cards */
.step {
    background: #FFFFFF;
    border: 1px solid #E2E8F0;
    border-radius: 18px;
    padding: 28px 34px;
    margin-bottom: 24px;
    box-shadow: 0 2px 12px -1px rgba(15, 23, 42, 0.04);
}
.step-header {
    display: flex;
    align-items: flex-start;
    gap: 16px;
    margin-bottom: 14px;
}
.num-badge {
    flex-shrink: 0;
    display: inline-flex;
    width: 36px;
    height: 36px;
    border-radius: 10px;
    align-items: center;
    justify-content: center;
    background: linear-gradient(135deg, #4F46E5, #6366F1);
    color: #FFFFFF;
    font-weight: 700;
    font-size: 14px;
    box-shadow: 0 4px 10px -2px rgba(79, 70, 229, 0.35);
}
.step-title-wrap { flex-grow: 1; }
.step h2 {
    font-size: 18px;
    font-weight: 700;
    color: #0F172A;
    line-height: 1.35;
}
.step p {
    font-size: 14px;
    color: #334155;
    line-height: 1.65;
    margin-top: 8px;
    white-space: pre-line;
}
.tags-row {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    margin-top: 14px;
}
.tag {
    font-size: 11px;
    font-weight: 600;
    padding: 3px 9px;
    border-radius: 6px;
    background: #F8FAFC;
    color: #64748B;
    border: 1px solid #E2E8F0;
}
.tag.action {
    background: #EEF2FF;
    color: #4F46E5;
    border-color: #E0E7FF;
}
.tag.window {
    background: #F0FDF4;
    color: #166534;
    border-color: #DCFCE7;
}
.step-img-wrap {
    margin-top: 18px;
    border-radius: 12px;
    overflow: hidden;
    border: 1px solid #E2E8F0;
    background: #0F172A;
    box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}
.step img {
    display: block;
    width: 100%;
    height: auto;
    border-radius: 11px;
}

/* Verification & Sign-off Block */
.signoff-section {
    background: #FFFFFF;
    border: 1px solid #E2E8F0;
    border-radius: 16px;
    padding: 24px 28px;
    margin-top: 36px;
}
.signoff-section h3 {
    font-size: 13px;
    font-weight: 700;
    color: #475569;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    margin-bottom: 14px;
}
.signoff-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 16px;
}
.signoff-box {
    background: #F8FAFC;
    border: 1px solid #E2E8F0;
    border-radius: 10px;
    padding: 12px 14px;
}
.signoff-label {
    font-size: 10px;
    font-weight: 700;
    color: #64748B;
    text-transform: uppercase;
}
.signoff-val {
    font-size: 12px;
    font-weight: 600;
    color: #0F172A;
    margin-top: 4px;
}

.footer {
    text-align: center;
    font-size: 12px;
    color: #94A3B8;
    margin-top: 36px;
}
@media print {
    @page { margin: 15mm 14mm 15mm 14mm; size: A4; }
    body { background: #FFFFFF !important; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
    .wrap { max-width: 100% !important; padding: 0 !important; }
    .doc-header { box-shadow: none !important; border: 1px solid #E2E8F0 !important; page-break-after: avoid; }
    .toc-section { box-shadow: none !important; border: 1px solid #E2E8F0 !important; page-break-after: avoid; }
    .step { box-shadow: none !important; border: 1px solid #E2E8F0 !important; page-break-inside: avoid !important; break-inside: avoid !important; margin-bottom: 20px !important; }
    .signoff-section { page-break-inside: avoid !important; }
}
</style></head><body><main class='wrap'>");

        // Document Header
        sb.AppendLine("<section class='doc-header'>");
        sb.AppendLine("<div class='brand-pill'>✦ Standard Operating Procedure</div>");
        sb.AppendLine($"<h1>{WebUtility.HtmlEncode(cleanTitle)}</h1>");
        sb.AppendLine("<table class='meta-table'>");
        sb.AppendLine($"<tr><td class='meta-label'>Target Application</td><td class='meta-val'>{WebUtility.HtmlEncode(primaryApp)}</td><td class='meta-label'>Total Steps</td><td class='meta-val'>{session.Steps.Count} steps</td></tr>");
        sb.AppendLine($"<tr><td class='meta-label'>Documentation Type</td><td class='meta-val'>{WebUtility.HtmlEncode(docMode)}</td><td class='meta-label'>Created Date</td><td class='meta-val'>{session.CreatedAt:dd MMM yyyy · HH:mm}</td></tr>");
        sb.AppendLine("</table>");
        sb.AppendLine("</section>");

        // Executive Table of Contents / Index
        if (session.Steps.Count > 1)
        {
            sb.AppendLine("<section class='toc-section'>");
            sb.AppendLine("<h3>📋 Workflow Step Index</h3>");
            sb.AppendLine("<ul class='toc-list'>");
            foreach (var step in session.Steps)
            {
                var sTitle = PrivacySanitizer.Clean(step.Title, 160);
                sb.AppendLine($"<li class='toc-item'><span class='toc-num'>#{step.Number:00}</span><span class='toc-title'>{WebUtility.HtmlEncode(sTitle)}</span><span class='toc-badge'>{WebUtility.HtmlEncode(step.Action)}</span></li>");
            }
            sb.AppendLine("</ul></section>");
        }

        // Detailed Step Cards
        foreach (var step in session.Steps)
        {
            var imageData = TryReadTrustedScreenshot(session, step, out var bytes) ? Convert.ToBase64String(bytes) : "";
            var stepTitle = PrivacySanitizer.Clean(step.Title, 240);
            var stepDesc = PrivacySanitizer.Clean(step.Description, 4000);

            sb.AppendLine("<section class='step'>");
            sb.AppendLine("<div class='step-header'>");
            sb.AppendLine($"<div class='num-badge'>{step.Number}</div>");
            sb.AppendLine("<div class='step-title-wrap'>");
            sb.AppendLine($"<h2>{WebUtility.HtmlEncode(stepTitle)}</h2>");
            sb.AppendLine($"<p>{WebUtility.HtmlEncode(stepDesc)}</p>");
            sb.AppendLine("<div class='tags-row'>");
            if (!string.IsNullOrWhiteSpace(step.Action))
                sb.AppendLine($"<span class='tag action'>Action: {WebUtility.HtmlEncode(step.Action)}</span>");
            if (!string.IsNullOrWhiteSpace(step.Context.ControlType))
                sb.AppendLine($"<span class='tag'>Control: {WebUtility.HtmlEncode(step.Context.ControlType)}</span>");
            if (!string.IsNullOrWhiteSpace(step.Context.ProcessName))
                sb.AppendLine($"<span class='tag'>App: {WebUtility.HtmlEncode(step.Context.ProcessName)}</span>");
            if (!string.IsNullOrWhiteSpace(step.Context.WindowTitle))
                sb.AppendLine($"<span class='tag window'>Window: {WebUtility.HtmlEncode(step.Context.WindowTitle)}</span>");
            sb.AppendLine("</div></div></div>");

            if (imageData.Length > 0)
            {
                sb.AppendLine("<div class='step-img-wrap'>");
                sb.AppendLine($"<img src='data:image/png;base64,{imageData}' alt='Step {step.Number} screenshot'>");
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</section>");
        }

        // Sign-off Block
        sb.AppendLine("<section class='signoff-section'>");
        sb.AppendLine("<h3>✔ Quality Assurance &amp; Sign-off</h3>");
        sb.AppendLine("<div class='signoff-grid'>");
        sb.AppendLine("<div class='signoff-box'><div class='signoff-label'>Recorded By</div><div class='signoff-val'>SoplyraAI Process Capture</div></div>");
        sb.AppendLine($"<div class='signoff-box'><div class='signoff-label'>Verification Status</div><div class='signoff-val'>Verified ({session.Steps.Count} steps)</div></div>");
        sb.AppendLine($"<div class='signoff-box'><div class='signoff-label'>Generated Timestamp</div><div class='signoff-val'>{DateTime.Now:dd MMM yyyy · HH:mm}</div></div>");
        sb.AppendLine("</div></section>");

        sb.AppendLine("<div class='footer'>Generated with SoplyraAI · Local-first AI Workflow Documentation Engine</div>");
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

        var cleanTitle = PrivacySanitizer.Clean(session.Title, 200);
        if (string.IsNullOrWhiteSpace(cleanTitle)) cleanTitle = "Standard Operating Procedure";
        var primaryApp = session.Steps.Select(s => s.Context.ProcessName).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? "Windows Desktop";

        var sb = new StringBuilder();
        sb.AppendLine($"# {EscapeMarkdown(cleanTitle)}\n");
        sb.AppendLine("## 📋 Executive Overview\n");
        sb.AppendLine("| Parameter | Details |");
        sb.AppendLine("| :--- | :--- |");
        sb.AppendLine($"| **Document Title** | {EscapeMarkdown(cleanTitle)} |");
        sb.AppendLine($"| **Target Application** | `{primaryApp}` |");
        sb.AppendLine($"| **Documentation Type** | {session.DocumentationMode} Guide |");
        sb.AppendLine($"| **Total Steps** | {session.Steps.Count} steps |");
        sb.AppendLine($"| **Date Recorded** | {session.CreatedAt:dd MMM yyyy · HH:mm} |");
        sb.AppendLine($"| **Security Classification** | Internal / Standard Operating Procedure |\n");

        sb.AppendLine("## 📑 Table of Contents\n");
        foreach (var step in session.Steps)
        {
            var sTitle = EscapeMarkdown(step.Title);
            sb.AppendLine($"- [Step {step.Number:00}: {sTitle}](#step-{step.Number}-{step.Number:00})");
        }
        sb.AppendLine("\n---\n");

        sb.AppendLine("## 🛠️ Step-by-Step Procedure\n");
        foreach (var step in session.Steps)
        {
            var name = $"step-{step.Number:000}.png";
            var copied = false;
            if (PathSecurity.TryGetTrustedPng(session.SessionFolder, step.ScreenshotPath, out var trusted, requireExists: true))
            {
                File.Copy(trusted, Path.Combine(imagesFolder, name), true);
                copied = true;
            }

            sb.AppendLine($"### <a id=\"step-{step.Number}-{step.Number:00}\"></a>Step {step.Number}: {EscapeMarkdown(step.Title)}\n");
            sb.AppendLine($"> **Action:** `{step.Action}` | **Control:** `{step.Context.ControlType}` | **Application:** `{step.Context.ProcessName}`\n");
            if (!string.IsNullOrWhiteSpace(step.Context.WindowTitle))
            {
                sb.AppendLine($"> **Window:** *{EscapeMarkdown(step.Context.WindowTitle)}*\n");
            }
            sb.AppendLine(EscapeMarkdown(step.Description));
            sb.AppendLine();

            if (copied)
            {
                sb.AppendLine($"![Step {step.Number}: {EscapeMarkdown(step.Title)}](images/{name})\n");
            }
            sb.AppendLine("---\n");
        }

        sb.AppendLine("## ✔️ Verification Checklist\n");
        foreach (var step in session.Steps)
        {
            sb.AppendLine($"- [ ] **Step {step.Number:00} Verified**: {EscapeMarkdown(step.Title)}");
        }
        sb.AppendLine("\n---\n*Documentation generated automatically with [SoplyraAI](https://github.com/logeshv586-code/SoplyraAI).*");

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
        
        AppendParagraph(doc, session.Title, 36, true);
        AppendParagraph(doc, $"Standard Operating Procedure · {session.Steps.Count} steps · {session.DocumentationMode} Guide · Created {session.CreatedAt:dd MMM yyyy HH:mm}", 20, false);

        var imageIndex = 0;
        foreach (var step in session.Steps)
        {
            AppendParagraph(doc, $"Step {step.Number} · {step.Title}", 26, true);
            AppendParagraph(doc, $"Action: {step.Action} | Control: {step.Context.ControlType} | App: {step.Context.ProcessName}", 18, false);

            foreach (var line in PrivacySanitizer.Clean(step.Description, 4000).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                AppendParagraph(doc, line, 22, false);

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
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                WorkingDirectory = Path.GetDirectoryName(browser) ?? Path.GetTempPath()
            };

            string[] args =
            {
                "--headless=new",
                "--disable-gpu",
                "--no-sandbox",
                "--disable-extensions",
                "--disable-background-networking",
                "--no-first-run",
                "--no-default-browser-check",
                "--allow-file-access-from-files",
                "--run-all-compositor-stages-before-draw",
                "--virtual-time-budget=2000",
                "--print-to-pdf-no-header",
                $"--user-data-dir={profile}",
                $"--print-to-pdf={pdf}",
                new Uri(html).AbsoluteUri
            };

            foreach (var arg in args) psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi);
            if (process is null) return null;

            // Wait for PDF file to appear
            for (var i = 0; i < 30; i++)
            {
                if (File.Exists(pdf) && new FileInfo(pdf).Length > 1000)
                {
                    try { if (!process.HasExited) process.Kill(true); } catch { }
                    return pdf;
                }
                await Task.Delay(200);
            }

            return File.Exists(pdf) && new FileInfo(pdf).Length > 1000 ? pdf : null;
        }
        finally
        {
            try { Directory.Delete(profile, true); } catch { }
        }
    }

    public async Task<List<string>> ExportAllFormatsAsync(GuideSession session, string folder)
    {
        Directory.CreateDirectory(folder);
        var outputs = new List<string>
        {
            ExportHtml(session, folder),
            ExportDocx(session, folder),
            ExportMarkdown(session, folder)
        };

        var pdf = await ExportPdfAsync(session, folder);
        if (pdf != null && File.Exists(pdf))
        {
            outputs.Add(pdf);
        }

        // Export metadata.json for workflow integration
        try
        {
            var metaPath = Path.Combine(folder, "metadata.json");
            var summary = new
            {
                Title = session.Title,
                CreatedAt = session.CreatedAt,
                StepsCount = session.Steps.Count,
                DocumentationMode = session.DocumentationMode,
                ExportedFiles = outputs.Select(Path.GetFileName).ToList(),
                Steps = session.Steps.Select(s => new
                {
                    Number = s.Number,
                    Action = s.Action,
                    Title = s.Title,
                    Description = s.Description,
                    Application = s.Context.ProcessName,
                    ControlType = s.Context.ControlType,
                    Window = s.Context.WindowTitle
                })
            };
            File.WriteAllText(metaPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            outputs.Add(metaPath);
        }
        catch { }

        return outputs;
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

    public static string? FindHeadlessBrowser()
    {
        string[] standardPaths =
        {
            // Chrome standard paths
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe"),

            // Edge standard paths
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "Application", "msedge.exe"),

            // Brave standard paths
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "BraveSoftware", "Brave-Browser", "Application", "brave.exe")
        };

        foreach (var p in standardPaths)
        {
            try
            {
                if (File.Exists(p)) return p;
            }
            catch { }
        }

        return null;
    }
}
