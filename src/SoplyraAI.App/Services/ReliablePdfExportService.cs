using System.Diagnostics;
using System.Text;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

public sealed class ReliablePdfExportService
{
    private readonly ExportService _exporter;

    public ReliablePdfExportService(ExportService exporter)
    {
        _exporter = exporter;
    }

    public async Task<string?> ExportAsync(GuideSession session, string folder, CancellationToken cancellationToken = default)
    {
        if (session.Steps.Count == 0)
            throw new InvalidOperationException("The guide does not contain any recorded steps to export.");

        Directory.CreateDirectory(folder);
        var html = _exporter.ExportHtml(session, folder);
        var pdf = Path.Combine(folder, "guide.pdf");

        foreach (var browser in FindBrowsers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (File.Exists(pdf)) File.Delete(pdf);
                if (await TryBrowserAsync(browser, html, pdf, cancellationToken))
                    return pdf;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Continue with the next trusted installed browser.
            }
        }

        return null;
    }

    private static async Task<bool> TryBrowserAsync(
        string browser,
        string html,
        string pdf,
        CancellationToken cancellationToken)
    {
        var profile = Path.Combine(Path.GetTempPath(), "soplyraai-pdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profile);
        Process? process = null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = browser,
                UseShellExecute = false,
                CreateNoWindow = true,
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

            process = Process.Start(psi);
            if (process is null) return false;

            long previousLength = -1;
            var stableReads = 0;
            var deadline = DateTime.UtcNow.AddSeconds(30);

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (File.Exists(pdf))
                {
                    long length;
                    try { length = new FileInfo(pdf).Length; }
                    catch { length = 0; }

                    if (length >= 4096 && length == previousLength)
                        stableReads++;
                    else
                        stableReads = 0;

                    previousLength = length;

                    // Do not terminate Chromium merely because the file exists.
                    // Require repeated stable size readings plus a complete PDF trailer.
                    if (stableReads >= 3 && IsCompletePdf(pdf))
                    {
                        TryStop(process);
                        return true;
                    }
                }

                if (process.HasExited)
                    return IsCompletePdf(pdf);

                await Task.Delay(250, cancellationToken);
            }

            TryStop(process);
            return IsCompletePdf(pdf);
        }
        finally
        {
            if (process is not null) TryStop(process);
            try { Directory.Delete(profile, true); } catch { }
        }
    }

    internal static bool IsCompletePdf(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < 4096) return false;

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Span<byte> header = stackalloc byte[5];
            if (stream.Read(header) != header.Length || Encoding.ASCII.GetString(header) != "%PDF-")
                return false;

            var tailLength = (int)Math.Min(4096, stream.Length);
            stream.Seek(-tailLength, SeekOrigin.End);
            var tail = new byte[tailLength];
            var read = stream.Read(tail, 0, tail.Length);
            return Encoding.ASCII.GetString(tail, 0, read).Contains("%EOF", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<string> FindBrowsers()
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

    private static void TryStop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }
        }
        catch
        {
        }
    }
}
