using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

public sealed class ExportService
{
    private const long MaxImageCx = 5_800_000;
    private const long MaxImageCy = 4_650_000;

    public string ExportHtml(GuideSession session, string folder)
    {
        ValidateSessionForExport(session);
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, "guide.html");
        var title = CleanTitle(session.Title);
        var apps = GetApplications(session);
        var mode = session.DocumentationMode == "Detailed" ? "Detailed SOP" : "Quick Visual Guide";
        var purpose = BuildDocumentPurpose(session, apps);
        var scope = BuildDocumentScope(session);
        var prerequisites = BuildPrerequisites(apps);
        var completion = BuildCompletionCriteria(session);
        var steps = session.Steps.Select(step => BuildStepContent(session, step)).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>");
        sb.AppendLine("<meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\">");
        sb.AppendLine("<meta name='referrer' content='no-referrer'>");
        sb.AppendLine($"<title>{H(title)} — SoplyraAI SOP</title>");
        sb.AppendLine(@"<style>
*{box-sizing:border-box}body{margin:0;background:#f7f9fc;color:#0f172a;font-family:Segoe UI,Arial,sans-serif;line-height:1.55}.wrap{max-width:1000px;margin:0 auto;padding:44px 28px 72px}.card{background:#fff;border:1px solid #e2e8f0;border-radius:18px;box-shadow:0 8px 28px rgba(15,23,42,.05)}.header{padding:34px 38px;margin-bottom:22px}.eyebrow{display:inline-block;padding:5px 10px;border-radius:999px;background:#eef2ff;color:#4f46e5;font-size:11px;font-weight:700;letter-spacing:.05em;text-transform:uppercase}.header h1{font-size:30px;line-height:1.2;margin:14px 0 18px}.meta{display:grid;grid-template-columns:1fr 1fr;gap:10px 18px;padding:16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:12px}.meta div{font-size:12px}.meta b{display:block;color:#64748b;font-size:10px;text-transform:uppercase;letter-spacing:.05em;margin-bottom:2px}.overview{padding:28px 32px;margin-bottom:22px}.overview h2,.index h2{font-size:17px;margin:0 0 16px}.overview-grid{display:grid;grid-template-columns:1fr 1fr;gap:14px}.info{background:#f8fafc;border:1px solid #e2e8f0;border-radius:12px;padding:15px}.info h3{font-size:12px;color:#4f46e5;margin:0 0 6px;text-transform:uppercase;letter-spacing:.04em}.info p{margin:0;font-size:13px;color:#334155}.index{padding:24px 30px;margin-bottom:24px}.index ol{margin:0;padding-left:24px}.index li{padding:5px 0;font-size:13px;color:#334155}.step{padding:28px 32px;margin:0 0 24px}.step-head{display:flex;gap:14px;align-items:flex-start;margin-bottom:18px}.num{width:38px;height:38px;flex:0 0 38px;border-radius:11px;background:linear-gradient(135deg,#4f46e5,#3b82f6);color:white;font-weight:800;display:flex;align-items:center;justify-content:center}.step h2{font-size:19px;line-height:1.35;margin:2px 0 0}.screen{margin:0 0 20px;border:1px solid #dbe2ea;border-radius:13px;background:#f1f5f9;overflow:hidden}.screen img{display:block;width:100%;height:auto;max-height:620px;object-fit:contain;background:#fff}.caption{padding:8px 12px;border-top:1px solid #e2e8f0;font-size:11px;color:#64748b;background:#f8fafc}.missing{padding:28px;text-align:center;color:#64748b;font-size:12px}.details{display:grid;grid-template-columns:1fr 1fr;gap:12px}.detail{border:1px solid #e2e8f0;border-radius:12px;padding:14px;background:#fff}.detail.wide{grid-column:1/-1}.detail h3{font-size:11px;text-transform:uppercase;letter-spacing:.05em;color:#64748b;margin:0 0 6px}.detail p{font-size:13px;color:#1e293b;margin:0;white-space:pre-line}.context{margin-top:14px;padding:12px 14px;background:#f8fafc;border-radius:10px;border:1px solid #e2e8f0;font-size:11px;color:#475569}.context span{display:inline-block;margin:3px 14px 3px 0}.finish{padding:24px 30px;margin-top:8px}.finish h2{font-size:16px;margin:0 0 8px}.finish p{margin:0;color:#334155;font-size:13px}.footer{text-align:center;color:#94a3b8;font-size:11px;margin-top:30px}@media(max-width:720px){.meta,.overview-grid,.details{grid-template-columns:1fr}.detail.wide{grid-column:auto}.wrap{padding:20px 12px}}@media print{@page{size:A4;margin:13mm}body{background:white;-webkit-print-color-adjust:exact;print-color-adjust:exact}.wrap{max-width:none;padding:0}.card{box-shadow:none}.header,.overview,.index,.finish{break-inside:avoid}.step{box-shadow:none;break-before:auto}.step h2{break-after:avoid}.screen{break-inside:avoid}.screen img{max-height:145mm;object-fit:contain}.details{break-inside:avoid}.footer{margin-top:18px}}
</style></head><body><main class='wrap'>");

        sb.AppendLine("<section class='card header'>");
        sb.AppendLine("<span class='eyebrow'>Standard Operating Procedure</span>");
        sb.AppendLine($"<h1>{H(title)}</h1>");
        sb.AppendLine("<div class='meta'>");
        sb.AppendLine($"<div><b>Applications</b>{H(apps)}</div>");
        sb.AppendLine($"<div><b>Documentation type</b>{H(mode)}</div>");
        sb.AppendLine($"<div><b>Total steps</b>{session.Steps.Count}</div>");
        sb.AppendLine($"<div><b>Recorded</b>{session.CreatedAt:dd MMM yyyy · HH:mm}</div>");
        sb.AppendLine("</div></section>");

        sb.AppendLine("<section class='card overview'><h2>Document overview</h2><div class='overview-grid'>");
        AppendInfo(sb, "Purpose", purpose);
        AppendInfo(sb, "Scope", scope);
        AppendInfo(sb, "Prerequisites", prerequisites);
        AppendInfo(sb, "Completion criteria", completion);
        sb.AppendLine("</div></section>");

        if (steps.Count > 1)
        {
            sb.AppendLine("<section class='card index'><h2>Workflow step index</h2><ol>");
            foreach (var step in steps)
                sb.AppendLine($"<li><b>Step {step.Number:00}</b> — {H(step.Title)}</li>");
            sb.AppendLine("</ol></section>");
        }

        foreach (var step in steps)
        {
            sb.AppendLine("<section class='card step'>");
            sb.AppendLine($"<div class='step-head'><div class='num'>{step.Number}</div><div><h2>{H(step.Title)}</h2></div></div>");

            if (step.ImageBytes.Length > 0)
            {
                var imageData = Convert.ToBase64String(step.ImageBytes);
                sb.AppendLine("<figure class='screen'>");
                sb.AppendLine($"<img src='data:image/png;base64,{imageData}' alt='Captured screen for step {step.Number}'>");
                sb.AppendLine($"<figcaption class='caption'>Captured screen for Step {step.Number}. The highlighted/selected control is explained below.</figcaption></figure>");
            }
            else
            {
                sb.AppendLine("<div class='screen'><div class='missing'>Screenshot not available in this local session. The captured UI context is documented below.</div></div>");
            }

            sb.AppendLine("<div class='details'>");
            AppendDetail(sb, "How to perform", step.Instruction);
            AppendDetail(sb, "What this does", step.Purpose);
            AppendDetail(sb, "Expected result", step.ExpectedResult, wide: true);
            sb.AppendLine("</div>");
            sb.AppendLine("<div class='context'>");
            sb.AppendLine($"<span><b>Action:</b> {H(step.Action)}</span><span><b>Control:</b> {H(step.Control)}</span><span><b>Application:</b> {H(step.Application)}</span>");
            if (!string.IsNullOrWhiteSpace(step.Window)) sb.AppendLine($"<span><b>Window:</b> {H(step.Window)}</span>");
            sb.AppendLine("</div></section>");
        }

        sb.AppendLine("<section class='card finish'><h2>Procedure completion</h2>");
        sb.AppendLine($"<p>{H(completion)} Review the captured screen and expected result for each step before publishing or sharing this document.</p></section>");
        sb.AppendLine("<div class='footer'>Generated locally with SoplyraAI · Screenshots remain in the guide unless you choose to share the export.</div>");
        sb.AppendLine("</main></body></html>");

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        ValidateHtml(path, steps.Count);
        return path;
    }

    public string ExportMarkdown(GuideSession session, string folder)
    {
        ValidateSessionForExport(session);
        Directory.CreateDirectory(folder);
        var imagesFolder = Path.Combine(folder, "images");
        Directory.CreateDirectory(imagesFolder);
        var path = Path.Combine(folder, "guide.md");

        var title = CleanTitle(session.Title);
        var apps = GetApplications(session);
        var steps = session.Steps.Select(step => BuildStepContent(session, step)).ToList();
        var sb = new StringBuilder();

        sb.AppendLine($"# {EscapeMarkdown(title)}");
        sb.AppendLine();
        sb.AppendLine("## Document overview");
        sb.AppendLine();
        sb.AppendLine($"- **Purpose:** {EscapeMarkdown(BuildDocumentPurpose(session, apps))}");
        sb.AppendLine($"- **Scope:** {EscapeMarkdown(BuildDocumentScope(session))}");
        sb.AppendLine($"- **Prerequisites:** {EscapeMarkdown(BuildPrerequisites(apps))}");
        sb.AppendLine($"- **Completion criteria:** {EscapeMarkdown(BuildCompletionCriteria(session))}");
        sb.AppendLine($"- **Applications:** {EscapeMarkdown(apps)}");
        sb.AppendLine($"- **Documentation type:** {(session.DocumentationMode == "Detailed" ? "Detailed SOP" : "Quick Visual Guide")}");
        sb.AppendLine($"- **Total steps:** {steps.Count}");
        sb.AppendLine();

        sb.AppendLine("## Workflow step index");
        sb.AppendLine();
        foreach (var step in steps) sb.AppendLine($"- Step {step.Number:00}: {EscapeMarkdown(step.Title)}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var step in steps)
        {
            sb.AppendLine($"## Step {step.Number}: {EscapeMarkdown(step.Title)}");
            sb.AppendLine();

            if (step.ImageBytes.Length > 0)
            {
                var name = $"step-{step.Number:000}.png";
                File.WriteAllBytes(Path.Combine(imagesFolder, name), step.ImageBytes);
                sb.AppendLine($"![Captured screen for Step {step.Number}](images/{name})");
                sb.AppendLine();
                sb.AppendLine($"*Captured screen for Step {step.Number}. The selected control is explained below.*");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("> Screenshot not available in this local session.");
                sb.AppendLine();
            }

            sb.AppendLine("### How to perform");
            sb.AppendLine();
            sb.AppendLine(EscapeMarkdown(step.Instruction));
            sb.AppendLine();
            sb.AppendLine("### What this does");
            sb.AppendLine();
            sb.AppendLine(EscapeMarkdown(step.Purpose));
            sb.AppendLine();
            sb.AppendLine("### Expected result");
            sb.AppendLine();
            sb.AppendLine(EscapeMarkdown(step.ExpectedResult));
            sb.AppendLine();
            sb.AppendLine($"**Context:** Action `{EscapeMarkdown(step.Action)}` · Control `{EscapeMarkdown(step.Control)}` · Application `{EscapeMarkdown(step.Application)}`{(string.IsNullOrWhiteSpace(step.Window) ? "" : $" · Window `{EscapeMarkdown(step.Window)}`")}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        sb.AppendLine("## Procedure completion");
        sb.AppendLine();
        sb.AppendLine(EscapeMarkdown(BuildCompletionCriteria(session)));
        sb.AppendLine();
        sb.AppendLine("Review each screenshot and expected result before sharing the final procedure.");

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        ValidateMarkdown(path, steps.Count);
        return path;
    }

    public string ExportDocx(GuideSession session, string folder)
    {
        ValidateSessionForExport(session);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "guide.docx");
        if (File.Exists(path)) File.Delete(path);

        var title = CleanTitle(session.Title);
        var apps = GetApplications(session);
        var steps = session.Steps.Select(step => BuildStepContent(session, step)).ToList();

        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
            WriteEntry(archive, "_rels/.rels", BuildPackageRelationshipsXml());
            WriteEntry(archive, "docProps/core.xml", BuildCorePropertiesXml(title));
            WriteEntry(archive, "docProps/app.xml", BuildAppPropertiesXml());
            WriteEntry(archive, "word/styles.xml", BuildStylesXml());

            var doc = new StringBuilder();
            var rels = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rIdStyles\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");

            doc.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><w:body>");

            AppendParagraph(doc, title, styleId: "Title");
            AppendParagraph(doc, "Standard Operating Procedure generated from a captured Windows workflow.", styleId: "Subtitle");
            AppendParagraph(doc, $"Applications: {apps}   |   Documentation type: {(session.DocumentationMode == "Detailed" ? "Detailed SOP" : "Quick Visual Guide")}   |   Steps: {steps.Count}   |   Recorded: {session.CreatedAt:dd MMM yyyy HH:mm}", styleId: "Meta");

            AppendParagraph(doc, "Document overview", styleId: "Heading1");
            AppendLabeledParagraph(doc, "Purpose", BuildDocumentPurpose(session, apps));
            AppendLabeledParagraph(doc, "Scope", BuildDocumentScope(session));
            AppendLabeledParagraph(doc, "Prerequisites", BuildPrerequisites(apps));
            AppendLabeledParagraph(doc, "Completion criteria", BuildCompletionCriteria(session));

            if (steps.Count > 1)
            {
                AppendParagraph(doc, "Workflow step index", styleId: "Heading1");
                foreach (var step in steps) AppendParagraph(doc, $"Step {step.Number:00} — {step.Title}", styleId: "BodyText");
            }

            var imageIndex = 0;
            foreach (var step in steps)
            {
                if (step.Number > 1 && session.DocumentationMode == "Detailed") AppendPageBreak(doc);
                AppendParagraph(doc, $"Step {step.Number}: {step.Title}", styleId: "Heading1", keepNext: true);

                if (step.ImageBytes.Length > 0)
                {
                    imageIndex++;
                    var fileName = $"image{imageIndex}.png";
                    var relId = $"rIdImg{imageIndex}";
                    var media = archive.CreateEntry("word/media/" + fileName, CompressionLevel.Optimal);
                    using (var output = media.Open()) output.Write(step.ImageBytes, 0, step.ImageBytes.Length);
                    rels.Append($"<Relationship Id=\"{relId}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"media/{fileName}\"/>");
                    var (cx, cy) = GetPngExtent(step.ImageBytes);
                    AppendImage(doc, relId, imageIndex, cx, cy);
                    AppendParagraph(doc, $"Captured screen for Step {step.Number}. The selected control is explained below.", styleId: "Caption");
                }
                else
                {
                    AppendParagraph(doc, "Screenshot not available in this local session. The captured UI context is documented below.", styleId: "Caption");
                }

                AppendParagraph(doc, "How to perform", styleId: "Heading2", keepNext: true);
                AppendParagraph(doc, step.Instruction, styleId: "BodyText");
                AppendParagraph(doc, "What this does", styleId: "Heading2", keepNext: true);
                AppendParagraph(doc, step.Purpose, styleId: "BodyText");
                AppendParagraph(doc, "Expected result", styleId: "Heading2", keepNext: true);
                AppendParagraph(doc, step.ExpectedResult, styleId: "BodyText");
                AppendParagraph(doc, "Context", styleId: "Heading2", keepNext: true);
                AppendParagraph(doc, $"Action: {step.Action}   |   Control: {step.Control}   |   Application: {step.Application}{(string.IsNullOrWhiteSpace(step.Window) ? "" : $"   |   Window: {step.Window}")}", styleId: "Meta");
            }

            AppendParagraph(doc, "Procedure completion", styleId: "Heading1");
            AppendParagraph(doc, BuildCompletionCriteria(session), styleId: "BodyText");
            AppendParagraph(doc, "Review each captured screen and expected result before publishing or sharing this procedure.", styleId: "BodyText");
            AppendParagraph(doc, "Generated locally with SoplyraAI.", styleId: "Caption");

            doc.Append("<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"850\" w:right=\"850\" w:bottom=\"850\" w:left=\"850\" w:header=\"400\" w:footer=\"400\"/></w:sectPr></w:body></w:document>");
            rels.Append("</Relationships>");

            WriteEntry(archive, "word/document.xml", doc.ToString());
            WriteEntry(archive, "word/_rels/document.xml.rels", rels.ToString());
        }

        ValidateDocx(path, steps.Count);
        return path;
    }

    public async Task<string?> ExportPdfAsync(GuideSession session, string folder)
    {
        ValidateSessionForExport(session);
        var html = ExportHtml(session, folder);
        var pdf = Path.Combine(folder, "guide.pdf");

        foreach (var browser in FindHeadlessBrowsers())
        {
            try
            {
                if (File.Exists(pdf)) File.Delete(pdf);
                if (await TryPrintPdfAsync(browser, html, pdf)) return pdf;
            }
            catch
            {
                // Try the next installed Chromium browser.
            }
        }

        return null;
    }

    public async Task<List<string>> ExportAllFormatsAsync(GuideSession session, string folder)
    {
        ValidateSessionForExport(session);
        Directory.CreateDirectory(folder);

        var outputs = new List<string>
        {
            ExportHtml(session, folder),
            ExportDocx(session, folder),
            ExportMarkdown(session, folder)
        };

        var pdf = await ExportPdfAsync(session, folder);
        if (!string.IsNullOrWhiteSpace(pdf) && IsHealthyPdf(pdf)) outputs.Add(pdf);

        var metadata = Path.Combine(folder, "metadata.json");
        var summary = new
        {
            Title = CleanTitle(session.Title),
            session.CreatedAt,
            StepsCount = session.Steps.Count,
            DocumentationMode = session.DocumentationMode,
            Purpose = BuildDocumentPurpose(session, GetApplications(session)),
            Scope = BuildDocumentScope(session),
            Prerequisites = BuildPrerequisites(GetApplications(session)),
            CompletionCriteria = BuildCompletionCriteria(session),
            ExportedFiles = outputs.Select(Path.GetFileName).ToList(),
            Steps = session.Steps.Select(step =>
            {
                var item = BuildStepContent(session, step);
                return new
                {
                    item.Number,
                    item.Title,
                    item.Instruction,
                    item.Purpose,
                    item.ExpectedResult,
                    item.Action,
                    item.Control,
                    item.Application,
                    item.Window,
                    HasScreenshot = item.ImageBytes.Length > 0
                };
            }).ToList()
        };
        File.WriteAllText(metadata, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        outputs.Add(metadata);
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

    public static string? FindHeadlessBrowser() => FindHeadlessBrowsers().FirstOrDefault();

    private static IEnumerable<string> FindHeadlessBrowsers()
    {
        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "BraveSoftware", "Brave-Browser", "Application", "brave.exe")
        };

        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<bool> TryPrintPdfAsync(string browser, string html, string pdf)
    {
        var profile = Path.Combine(Path.GetTempPath(), "soplyraai-pdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profile);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = browser,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(browser) ?? Path.GetTempPath()
            };

            string[] args =
            {
                "--headless=new",
                "--disable-gpu",
                "--disable-extensions",
                "--disable-background-networking",
                "--no-first-run",
                "--no-default-browser-check",
                "--run-all-compositor-stages-before-draw",
                "--virtual-time-budget=2500",
                "--print-to-pdf-no-header",
                $"--user-data-dir={profile}",
                $"--print-to-pdf={pdf}",
                new Uri(html).AbsoluteUri
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi);
            if (process is null) return false;

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return false;
            }

            _ = await stdoutTask;
            _ = await stderrTask;

            if (process.ExitCode != 0 && !File.Exists(pdf)) return false;
            return await WaitForStablePdfAsync(pdf);
        }
        finally
        {
            try { Directory.Delete(profile, true); } catch { }
        }
    }

    private static async Task<bool> WaitForStablePdfAsync(string pdf)
    {
        long previous = -1;
        var stableReads = 0;
        for (var i = 0; i < 20; i++)
        {
            if (File.Exists(pdf))
            {
                var length = new FileInfo(pdf).Length;
                if (length > 4096 && length == previous) stableReads++;
                else stableReads = 0;
                previous = length;
                if (stableReads >= 2 && IsHealthyPdf(pdf)) return true;
            }
            await Task.Delay(200);
        }
        return IsHealthyPdf(pdf);
    }

    private static bool IsHealthyPdf(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < 4096) return false;
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[5];
            if (stream.Read(header) != 5 || Encoding.ASCII.GetString(header) != "%PDF-") return false;

            var tailLength = (int)Math.Min(2048, stream.Length);
            stream.Seek(-tailLength, SeekOrigin.End);
            var tail = new byte[tailLength];
            _ = stream.Read(tail, 0, tail.Length);
            return Encoding.ASCII.GetString(tail).Contains("%EOF", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static StepExportContent BuildStepContent(GuideSession session, GuideStep step)
    {
        var context = step.Context ?? new UiContext();
        var action = PrivacySanitizer.Clean(step.Action, 40);
        if (string.IsNullOrWhiteSpace(action)) action = "Click";

        var control = PrivacySanitizer.Clean(
            !string.IsNullOrWhiteSpace(context.LocalizedControlType) ? context.LocalizedControlType : context.ControlType,
            100);
        if (string.IsNullOrWhiteSpace(control)) control = "control";

        var element = PrivacySanitizer.Clean(context.ElementName, 180);
        if (string.IsNullOrWhiteSpace(element)) element = HumanizeControl(control);

        var title = PrivacySanitizer.Clean(step.Title, 240);
        if (string.IsNullOrWhiteSpace(title)) title = $"{action} {element}";

        var application = PrivacySanitizer.Clean(context.ProcessName, 120);
        if (string.IsNullOrWhiteSpace(application)) application = "Target application";
        var window = PrivacySanitizer.Clean(context.WindowTitle, 240);

        var instruction = BuildInstruction(action, element, control);
        var purpose = PrivacySanitizer.Clean(step.Description, 4000);
        if (string.IsNullOrWhiteSpace(purpose)) purpose = BuildPurpose(element, title, control);
        var expected = BuildExpectedResult(element, title, purpose);

        var imageBytes = TryReadTrustedScreenshot(session, step, out var bytes) ? bytes : Array.Empty<byte>();
        return new StepExportContent(step.Number, title, instruction, purpose, expected, action, control, application, window, imageBytes);
    }

    private static string BuildInstruction(string action, string element, string control)
    {
        var target = control.Contains("button", StringComparison.OrdinalIgnoreCase)
            ? $"the “{element}” button"
            : control.Contains("tab", StringComparison.OrdinalIgnoreCase)
                ? $"the “{element}” tab"
                : control.Contains("menu", StringComparison.OrdinalIgnoreCase)
                    ? $"the “{element}” menu item"
                    : $"“{element}”";

        if (action.Equals("Right-click", StringComparison.OrdinalIgnoreCase)) return $"Right-click {target} to open its available context actions.";
        if (action.Equals("Middle-click", StringComparison.OrdinalIgnoreCase)) return $"Middle-click {target}.";
        if (action.Equals("Select", StringComparison.OrdinalIgnoreCase)) return $"Select {target}.";
        return $"Click {target}.";
    }

    private static string BuildPurpose(string element, string title, string control)
    {
        var text = $"{element} {title}".ToLowerInvariant();
        if (ContainsAny(text, "save", "apply")) return "This stores the current changes so the workflow can continue without losing the entered information.";
        if (ContainsAny(text, "submit", "send")) return "This sends the current information to the application for processing.";
        if (ContainsAny(text, "add", "create", "new")) return "This starts or creates a new item in the current workflow.";
        if (ContainsAny(text, "delete", "remove")) return "This removes the selected item from the current workflow, subject to any confirmation shown by the application.";
        if (ContainsAny(text, "search", "find")) return "This runs the current search or filter so matching results can be reviewed.";
        if (ContainsAny(text, "next", "continue")) return "This advances the workflow to the next stage or screen.";
        if (ContainsAny(text, "back", "previous")) return "This returns the workflow to the previous stage or screen.";
        if (ContainsAny(text, "login", "sign in", "log in")) return "This starts the sign-in action so the authorized workflow can continue.";
        if (control.Contains("tab", StringComparison.OrdinalIgnoreCase)) return "This switches the application to the selected section so its controls and information become available.";
        return "This activates the selected control and updates the application for the next recorded step.";
    }

    private static string BuildExpectedResult(string element, string title, string purpose)
    {
        var text = $"{element} {title} {purpose}".ToLowerInvariant();
        if (ContainsAny(text, "save", "apply")) return "The application should confirm or retain the saved changes, and the updated information should remain available.";
        if (ContainsAny(text, "submit", "send")) return "The application should submit the information and display the next stage, a confirmation, or a status update.";
        if (ContainsAny(text, "add", "create", "new")) return "A new item or entry should become available for the next part of the workflow.";
        if (ContainsAny(text, "delete", "remove")) return "The selected item should no longer appear after any required confirmation is completed.";
        if (ContainsAny(text, "search", "find")) return "The visible results should refresh to match the current search or filter criteria.";
        if (ContainsAny(text, "next", "continue")) return "The next screen or workflow stage should become visible.";
        if (ContainsAny(text, "back", "previous")) return "The previous screen or workflow stage should become visible.";
        if (ContainsAny(text, "login", "sign in", "log in")) return "The authorized application screen should become available if the sign-in is successful.";
        return "The interface should respond to the selected control and leave the application ready for the next recorded step.";
    }

    private static string CleanTitle(string? value)
    {
        var title = PrivacySanitizer.Clean(value, 200);
        return string.IsNullOrWhiteSpace(title) ? "Standard Operating Procedure" : title;
    }

    private static string GetApplications(GuideSession session)
    {
        var apps = session.Steps
            .Select(step => PrivacySanitizer.Clean(step.Context?.ProcessName, 120))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
        return apps.Count == 0 ? "Windows desktop application" : string.Join(", ", apps);
    }

    private static string BuildDocumentPurpose(GuideSession session, string apps) =>
        $"This document provides a repeatable, screen-by-screen procedure for the captured workflow in {apps}. It helps another user reproduce the process consistently by pairing each recorded screen with the action, purpose, and expected result.";

    private static string BuildDocumentScope(GuideSession session) =>
        $"The guide covers {session.Steps.Count} recorded user interaction{(session.Steps.Count == 1 ? "" : "s")} from the captured session. It documents visible workflow actions and UI context; typed passwords and credentials are not included.";

    private static string BuildPrerequisites(string apps) =>
        $"Open {apps}, use an account with the permissions required for the process, and navigate to the starting screen shown in Step 1 before following the instructions.";

    private static string BuildCompletionCriteria(GuideSession session) =>
        $"The procedure is complete when all {session.Steps.Count} recorded step{(session.Steps.Count == 1 ? "" : "s")} have been performed in order and the final expected result is visible in the application.";

    private static string HumanizeControl(string control)
    {
        var value = control.Replace("ControlType.", "", StringComparison.OrdinalIgnoreCase).Replace("_", " ").Trim();
        return string.IsNullOrWhiteSpace(value) ? "selected control" : value;
    }

    private static bool ContainsAny(string value, params string[] tokens) => tokens.Any(value.Contains);

    private static void ValidateSessionForExport(GuideSession session)
    {
        if (session is null) throw new ArgumentNullException(nameof(session));
        if (session.Steps is null || session.Steps.Count == 0)
            throw new InvalidOperationException("The guide does not contain any recorded steps to export.");
    }

    private static void ValidateHtml(string path, int expectedSteps)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < 1000) throw new InvalidOperationException("HTML export was generated without document content.");
        var text = File.ReadAllText(path, Encoding.UTF8);
        if (!text.Contains("Document overview", StringComparison.Ordinal) || !text.Contains("How to perform", StringComparison.Ordinal) || text.CountOccurrences("class='card step'") < expectedSteps)
            throw new InvalidOperationException("HTML export is missing one or more recorded steps.");
    }

    private static void ValidateMarkdown(string path, int expectedSteps)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < 250) throw new InvalidOperationException("Markdown export was generated without document content.");
        var text = File.ReadAllText(path, Encoding.UTF8);
        if (!text.Contains("## Document overview", StringComparison.Ordinal) || !text.Contains("### Expected result", StringComparison.Ordinal) || text.CountOccurrences("## Step ") < expectedSteps)
            throw new InvalidOperationException("Markdown export is missing one or more recorded steps.");
    }

    private static void ValidateDocx(string path, int expectedSteps)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < 1200) throw new InvalidOperationException("Word export was generated without document content.");
        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidOperationException("Word export is missing document.xml.");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        var xml = reader.ReadToEnd();
        if (!xml.Contains("Document overview", StringComparison.Ordinal) || !xml.Contains("Expected result", StringComparison.Ordinal) || xml.CountOccurrences("Step ") < expectedSteps)
            throw new InvalidOperationException("Word export is missing one or more recorded steps.");
    }

    private static bool TryReadTrustedScreenshot(GuideSession session, GuideStep step, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (!PathSecurity.TryGetTrustedPng(session.SessionFolder, step.ScreenshotPath, out var trusted, requireExists: true)) return false;
        try
        {
            bytes = File.ReadAllBytes(trusted);
            return bytes.Length >= 24;
        }
        catch
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }

    private static (long Cx, long Cy) GetPngExtent(byte[] png)
    {
        if (png.Length < 24) return (MaxImageCx, 3_262_500);
        var width = ReadBigEndianInt32(png, 16);
        var height = ReadBigEndianInt32(png, 20);
        if (width <= 0 || height <= 0) return (MaxImageCx, 3_262_500);

        var rawCx = Math.Max(1L, width * 9525L);
        var rawCy = Math.Max(1L, height * 9525L);
        var scale = Math.Min(1d, Math.Min(MaxImageCx / (double)rawCx, MaxImageCy / (double)rawCy));
        return ((long)(rawCx * scale), (long)(rawCy * scale));
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset) =>
        (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void AppendParagraph(StringBuilder sb, string? text, string? styleId = null, int? halfPoints = null, bool bold = false, bool keepNext = false)
    {
        var safe = Xml(PrivacySanitizer.Clean(text, 5000));
        sb.Append("<w:p>");
        if (!string.IsNullOrWhiteSpace(styleId) || keepNext)
        {
            sb.Append("<w:pPr>");
            if (!string.IsNullOrWhiteSpace(styleId)) sb.Append($"<w:pStyle w:val=\"{Xml(styleId)}\"/>");
            if (keepNext) sb.Append("<w:keepNext/>");
            sb.Append("</w:pPr>");
        }
        sb.Append("<w:r><w:rPr>");
        if (bold) sb.Append("<w:b/>");
        if (halfPoints.HasValue) sb.Append($"<w:sz w:val=\"{halfPoints.Value}\"/>");
        sb.Append($"</w:rPr><w:t xml:space=\"preserve\">{safe}</w:t></w:r></w:p>");
    }

    private static void AppendLabeledParagraph(StringBuilder sb, string label, string text)
    {
        sb.Append("<w:p><w:pPr><w:pStyle w:val=\"BodyText\"/></w:pPr><w:r><w:rPr><w:b/></w:rPr><w:t>");
        sb.Append(Xml(label + ": "));
        sb.Append("</w:t></w:r><w:r><w:t xml:space=\"preserve\">");
        sb.Append(Xml(PrivacySanitizer.Clean(text, 5000)));
        sb.Append("</w:t></w:r></w:p>");
    }

    private static void AppendPageBreak(StringBuilder sb) => sb.Append("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>");

    private static void AppendImage(StringBuilder sb, string relId, int id, long cx, long cy)
    {
        sb.Append($"<w:p><w:pPr><w:jc w:val=\"center\"/></w:pPr><w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/><wp:docPr id=\"{id + 10}\" name=\"Step screenshot {id}\"/><wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect=\"1\"/></wp:cNvGraphicFramePr><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic><pic:nvPicPr><pic:cNvPr id=\"{id + 10}\" name=\"Screenshot {id}\"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed=\"{relId}\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>");
    }

    private static string BuildContentTypesXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"png\" ContentType=\"image/png\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/><Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/><Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/><Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/></Types>";

    private static string BuildPackageRelationshipsXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/><Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/></Relationships>";

    private static string BuildCorePropertiesXml(string title)
    {
        var created = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"><dc:title>{Xml(title)}</dc:title><dc:creator>SoplyraAI</dc:creator><cp:lastModifiedBy>SoplyraAI</cp:lastModifiedBy><dcterms:created xsi:type=\"dcterms:W3CDTF\">{created}</dcterms:created><dcterms:modified xsi:type=\"dcterms:W3CDTF\">{created}</dcterms:modified></cp:coreProperties>";
    }

    private static string BuildAppPropertiesXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\"><Application>SoplyraAI</Application><AppVersion>1.0</AppVersion></Properties>";

    private static string BuildStylesXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:styles xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Segoe UI\" w:hAnsi=\"Segoe UI\"/><w:sz w:val=\"21\"/></w:rPr></w:rPrDefault></w:docDefaults><w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/><w:pPr><w:spacing w:after=\"120\" w:line=\"276\" w:lineRule=\"auto\"/></w:pPr></w:style><w:style w:type=\"paragraph\" w:styleId=\"Title\"><w:name w:val=\"Title\"/><w:pPr><w:spacing w:after=\"180\"/></w:pPr><w:rPr><w:b/><w:color w:val=\"0F172A\"/><w:sz w:val=\"38\"/></w:rPr></w:style><w:style w:type=\"paragraph\" w:styleId=\"Subtitle\"><w:name w:val=\"Subtitle\"/><w:pPr><w:spacing w:after=\"160\"/></w:pPr><w:rPr><w:color w:val=\"64748B\"/><w:sz w:val=\"22\"/></w:rPr></w:style><w:style w:type=\"paragraph\" w:styleId=\"Heading1\"><w:name w:val=\"heading 1\"/><w:pPr><w:keepNext/><w:spacing w:before=\"240\" w:after=\"120\"/></w:pPr><w:rPr><w:b/><w:color w:val=\"1E293B\"/><w:sz w:val=\"28\"/></w:rPr></w:style><w:style w:type=\"paragraph\" w:styleId=\"Heading2\"><w:name w:val=\"heading 2\"/><w:pPr><w:keepNext/><w:spacing w:before=\"160\" w:after=\"70\"/></w:pPr><w:rPr><w:b/><w:color w:val=\"4F46E5\"/><w:sz w:val=\"22\"/></w:rPr></w:style><w:style w:type=\"paragraph\" w:styleId=\"BodyText\"><w:name w:val=\"Body Text\"/><w:pPr><w:spacing w:after=\"120\" w:line=\"300\" w:lineRule=\"auto\"/></w:pPr><w:rPr><w:color w:val=\"334155\"/><w:sz w:val=\"21\"/></w:rPr></w:style><w:style w:type=\"paragraph\" w:styleId=\"Meta\"><w:name w:val=\"Meta\"/><w:pPr><w:spacing w:after=\"100\"/></w:pPr><w:rPr><w:color w:val=\"64748B\"/><w:sz w:val=\"18\"/></w:rPr></w:style><w:style w:type=\"paragraph\" w:styleId=\"Caption\"><w:name w:val=\"Caption\"/><w:pPr><w:jc w:val=\"center\"/><w:spacing w:after=\"140\"/></w:pPr><w:rPr><w:i/><w:color w:val=\"64748B\"/><w:sz w:val=\"18\"/></w:rPr></w:style></w:styles>";

    private static void AppendInfo(StringBuilder sb, string heading, string text) =>
        sb.AppendLine($"<div class='info'><h3>{H(heading)}</h3><p>{H(text)}</p></div>");

    private static void AppendDetail(StringBuilder sb, string heading, string text, bool wide = false) =>
        sb.AppendLine($"<div class='detail{(wide ? " wide" : "")}'><h3>{H(heading)}</h3><p>{H(text)}</p></div>");

    private static string H(string? value) => WebUtility.HtmlEncode(value ?? "");

    private static string Xml(string? value) => (value ?? "")
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&apos;");

    private static string EscapeMarkdown(string? value)
    {
        var text = PrivacySanitizer.Clean(value, 5000);
        var sb = new StringBuilder(text.Length + 16);
        foreach (var ch in text)
        {
            if ("\\`*_{}[]<>()#+-.!|".Contains(ch)) sb.Append('\\');
            sb.Append(ch);
        }
        return sb.ToString();
    }

    private sealed record StepExportContent(
        int Number,
        string Title,
        string Instruction,
        string Purpose,
        string ExpectedResult,
        string Action,
        string Control,
        string Application,
        string Window,
        byte[] ImageBytes);
}

internal static class ExportStringExtensions
{
    public static int CountOccurrences(this string value, string token)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(token)) return 0;
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }
}
