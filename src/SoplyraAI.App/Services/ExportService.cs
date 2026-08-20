using System.Diagnostics;
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
        sb.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;background:#f7f7fb;color:#16161f;margin:0}.wrap{max-width:980px;margin:0 auto;padding:48px 24px}.hero,.step{background:#fff;border:1px solid #e8e8ef;border-radius:18px;box-shadow:0 8px 30px rgba(20,20,40,.06)}.hero{padding:32px;margin-bottom:24px}.hero h1{margin:0 0 8px}.muted{color:#6b7280}.step{padding:20px;margin:18px 0}.step img{display:block;max-width:100%;border-radius:12px;border:1px solid #ececf2;margin-top:14px}.num{display:inline-flex;width:30px;height:30px;border-radius:50%;align-items:center;justify-content:center;background:#5b5bd6;color:white;font-weight:700;margin-right:10px}h2{font-size:19px}p{line-height:1.6}</style></head><body><main class='wrap'>");
        sb.AppendLine($"<section class='hero'><h1>{WebUtility.HtmlEncode(PrivacySanitizer.Clean(session.Title, 200))}</h1><div class='muted'>{session.Steps.Count} steps · Created {session.CreatedAt:dd MMM yyyy}</div></section>");

        foreach (var step in session.Steps)
        {
            var imageData = TryReadTrustedScreenshot(session, step, out var bytes)
                ? Convert.ToBase64String(bytes)
                : "";

            sb.AppendLine("<section class='step'>");
            sb.AppendLine($"<h2><span class='num'>{step.Number}</span>{WebUtility.HtmlEncode(PrivacySanitizer.Clean(step.Title, 240))}</h2>");
            sb.AppendLine($"<p>{WebUtility.HtmlEncode(PrivacySanitizer.Clean(step.Description, 4000))}</p>");
            if (imageData.Length > 0)
                sb.AppendLine($"<img src='data:image/png;base64,{imageData}' alt='Step {step.Number} screenshot'>");
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

        var sb = new StringBuilder(
            $"# {EscapeMarkdown(session.Title)}\n\n{session.Steps.Count} steps.\n\n");

        foreach (var step in session.Steps)
        {
            var name = $"step-{step.Number:000}.png";
            var copiedImage = false;

            if (PathSecurity.TryGetTrustedPng(
                    session.SessionFolder,
                    step.ScreenshotPath,
                    out var trusted,
                    requireExists: true))
            {
                File.Copy(trusted, Path.Combine(imagesFolder, name), true);
                copiedImage = true;
            }

            sb.AppendLine($"## {step.Number}. {EscapeMarkdown(step.Title)}");
            sb.AppendLine();
            sb.AppendLine(EscapeMarkdown(step.Description));
            sb.AppendLine();
            if (copiedImage)
                sb.AppendLine($"![Step {step.Number}](images/{name})");
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return path;
    }

    public async Task<string?> ExportPdfAsync(GuideSession session, string folder)
    {
        var html = ExportHtml(session, folder);
        var pdf = Path.Combine(folder, "guide.pdf");
        var edge = FindEdge();
        if (edge is null) return null;

        var psi = new ProcessStartInfo
        {
            FileName = edge,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(edge)!
        };

        psi.ArgumentList.Add("--headless");
        psi.ArgumentList.Add("--disable-gpu");
        psi.ArgumentList.Add("--disable-extensions");
        psi.ArgumentList.Add("--disable-background-networking");
        psi.ArgumentList.Add("--no-first-run");
        psi.ArgumentList.Add("--no-default-browser-check");
        psi.ArgumentList.Add($"--print-to-pdf={pdf}");
        psi.ArgumentList.Add(new Uri(html).AbsoluteUri);

        using var process = Process.Start(psi);
        if (process is null) return null;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return null;
        }

        return process.ExitCode == 0 && File.Exists(pdf) ? pdf : null;
    }

    public static string NewExportFolder(GuideSession session)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var cleanTitle = PrivacySanitizer.Clean(session.Title, 80);
        var safe = string.Concat(cleanTitle.Select(
            ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Trim();

        if (string.IsNullOrWhiteSpace(safe)) safe = "Guide";
        return Path.Combine(
            desktop,
            "SoplyraAI Exports",
            $"{safe}-{DateTime.Now:yyyyMMdd-HHmmss}");
    }

    private static bool TryReadTrustedScreenshot(
        GuideSession session,
        GuideStep step,
        out byte[] bytes)
    {
        bytes = Array.Empty<byte>();

        if (!PathSecurity.TryGetTrustedPng(
                session.SessionFolder,
                step.ScreenshotPath,
                out var trusted,
                requireExists: true))
            return false;

        try
        {
            bytes = File.ReadAllBytes(trusted);
            return true;
        }
        catch
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }

    private static string EscapeMarkdown(string? value)
    {
        var text = PrivacySanitizer.Clean(value, 4000);
        var sb = new StringBuilder(text.Length + 16);

        foreach (var ch in text)
        {
            if ("\\`*_{}[]<>()#+-.!|".Contains(ch))
                sb.Append('\\');
            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static string? FindEdge()
    {
        string[] paths =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe")
        };

        return paths.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
    }
}
