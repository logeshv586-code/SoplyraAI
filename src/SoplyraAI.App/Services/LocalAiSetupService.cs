using System.Diagnostics;

namespace SoplyraAI.Services;

public sealed class LocalAiSetupService
{
    public async Task<string> SetupAsync(Action<string>? log = null)
    {
        var ollama = FindOllama();
        if (ollama is null)
        {
            log?.Invoke("Ollama is not installed. Installing with winget…");
            var installCode = await RunAsync("winget", new[] { "install", "--id", "Ollama.Ollama", "-e", "--accept-package-agreements", "--accept-source-agreements" }, log);
            if (installCode != 0) return "Ollama installation did not complete. Install Ollama manually, then retry.";
            ollama = FindOllama();
            if (ollama is null) return "Ollama installed, but this process cannot locate it yet. Restart SoplyraAI and run setup again.";
        }

        log?.Invoke("Downloading the small local instruction model…");
        var pullCode = await RunAsync(ollama, new[] { "pull", "qwen2.5:0.5b" }, log);
        return pullCode == 0 ? "Local AI is ready: qwen2.5:0.5b" : "Ollama is installed, but the model download failed.";
    }

    private static string? FindOllama()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("where.exe", "ollama") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
            var first = p?.StandardOutput.ReadLine();
            p?.WaitForExit(2000);
            if (!string.IsNullOrWhiteSpace(first) && File.Exists(first)) return first;
        }
        catch { }

        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ollama", "ollama.exe")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static async Task<int> RunAsync(string file, IEnumerable<string> args, Action<string>? log)
    {
        try
        {
            var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in args) psi.ArgumentList.Add(arg);
            using var p = Process.Start(psi);
            if (p is null) return -1;
            p.OutputDataReceived += (_, e) => { if (e.Data is not null) log?.Invoke(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data is not null) log?.Invoke(e.Data); };
            p.BeginOutputReadLine(); p.BeginErrorReadLine();
            await p.WaitForExitAsync();
            return p.ExitCode;
        }
        catch (Exception ex) { log?.Invoke(ex.Message); return -1; }
    }
}
