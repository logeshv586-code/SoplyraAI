using System.Diagnostics;
using System.Security.Principal;

namespace SoplyraAI.Services;

public sealed class LocalAiSetupService
{
    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PullTimeout = TimeSpan.FromMinutes(30);

    public Task<string> SetupAsync(Action<string>? log = null) =>
        SetupAsync(AiProviderCatalog.Get("Ollama").DefaultModel, log);

    public async Task<string> SetupAsync(string modelName, Action<string>? log = null)
    {
        if (IsElevated())
            return "For safety, automatic AI setup is disabled while SoplyraAI is running as Administrator. Restart it normally and retry.";

        var model = AiProviderCatalog.LocalModels.FirstOrDefault(x => x.Equals(modelName, StringComparison.OrdinalIgnoreCase));
        if (model is null)
            return "That local model is not in SoplyraAI's trusted recommended-model list.";

        var ollama = FindOllama();
        if (ollama is null)
        {
            var winget = FindWinget();
            if (winget is null)
                return "Windows Package Manager was not found. Install Ollama manually, then retry.";

            log?.Invoke("Ollama is not installed. Installing with Windows Package Manager…");
            var installCode = await RunAsync(
                winget,
                new[] { "install", "--id", "Ollama.Ollama", "-e", "--accept-package-agreements", "--accept-source-agreements" },
                InstallTimeout,
                log);

            if (installCode != 0)
                return "Ollama installation did not complete. Install Ollama manually, then retry.";

            ollama = FindOllama();
            if (ollama is null)
                return "Ollama installed, but SoplyraAI cannot locate its trusted executable yet. Restart SoplyraAI and retry.";
        }

        log?.Invoke($"Downloading {model}…");
        var pullCode = await RunAsync(ollama, new[] { "pull", model }, PullTimeout, log);
        return pullCode == 0
            ? $"Local AI is ready: {model}"
            : "Ollama is installed, but the model download failed.";
    }

    private static string? FindOllama()
    {
        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ollama", "ollama.exe")
        };
        return candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
    }

    private static string? FindWinget()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
        return File.Exists(path) ? Path.GetFullPath(path) : null;
    }

    private static async Task<int> RunAsync(string file, IEnumerable<string> args, TimeSpan timeout, Action<string>? log)
    {
        try
        {
            if (!Path.IsPathFullyQualified(file) || !File.Exists(file))
            {
                log?.Invoke("Refusing to execute an untrusted helper path.");
                return -1;
            }

            var psi = new ProcessStartInfo(file)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(file)!
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi);
            if (process is null) return -1;
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) log?.Invoke(PrivacySanitizer.Clean(e.Data, 500)); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) log?.Invoke(PrivacySanitizer.Clean(e.Data, 500)); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = new CancellationTokenSource(timeout);
            try { await process.WaitForExitAsync(cts.Token); return process.ExitCode; }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                log?.Invoke("The helper process exceeded its allowed time and was stopped.");
                return -1;
            }
        }
        catch (Exception ex)
        {
            log?.Invoke(PrivacySanitizer.Clean(ex.Message, 500));
            return -1;
        }
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }
}
