using System.Diagnostics;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace SoplyraAI.Services;

public sealed record LocalModelStatus(
    bool OllamaInstalled,
    bool ServiceReady,
    bool ModelInstalled,
    IReadOnlyList<string> InstalledModels,
    string Message);

public sealed class LocalAiSetupService
{
    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PullTimeout = TimeSpan.FromMinutes(30);
    private static readonly Uri TagsEndpoint = new("http://127.0.0.1:11434/api/tags");

    public Task<string> SetupAsync(Action<string>? log = null) =>
        SetupAsync(AiProviderCatalog.Get("Ollama").DefaultModel, log);

    public async Task<string> SetupAsync(string modelName, Action<string>? log = null)
    {
        if (IsElevated())
            return "For safety, automatic AI setup is disabled while SoplyraAI is running as Administrator. Restart it normally and retry.";

        var model = ResolveTrustedModel(modelName);
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

        if (!await EnsureServerReadyAsync(ollama, log))
            return "Ollama is installed, but its local service could not be started. Close any stuck Ollama process, reopen SoplyraAI, and retry.";

        var installedBefore = await TryGetInstalledModelsAsync();
        if (installedBefore is not null && ContainsModel(installedBefore, model))
        {
            log?.Invoke($"Already installed: {model}. No download required.");
            log?.Invoke("Local Ollama service is ready.");
            return $"Local AI is ready: {model} (already installed)";
        }

        log?.Invoke($"Downloading {model}…");
        var pullCode = await RunAsync(ollama, new[] { "pull", model }, PullTimeout, log);
        if (pullCode != 0)
            return "Ollama is installed, but the model download failed.";

        if (!await EnsureServerReadyAsync(ollama, log))
            return $"{model} was downloaded, but the local Ollama service is not responding yet. Restart Ollama and use the installed model; do not download it again.";

        var installedAfter = await TryGetInstalledModelsAsync();
        if (installedAfter is not null && !ContainsModel(installedAfter, model))
            return $"Ollama reported a completed download, but {model} was not found in the installed-model list. Retry the model pull.";

        log?.Invoke($"Model download complete: {model}.");
        log?.Invoke("Local Ollama service is ready.");
        return $"Local AI is ready: {model}";
    }

    public async Task<LocalModelStatus> GetStatusAsync(string modelName, CancellationToken cancellationToken = default)
    {
        var model = ResolveTrustedModel(modelName);
        if (model is null)
        {
            return new LocalModelStatus(
                false,
                false,
                false,
                Array.Empty<string>(),
                "Choose a trusted SoplyraAI local model.");
        }

        var ollama = FindOllama();
        if (ollama is null)
        {
            return new LocalModelStatus(
                false,
                false,
                false,
                Array.Empty<string>(),
                "Ollama is not installed yet. SoplyraAI can install it when you download a local model.");
        }

        var ready = await EnsureServerReadyAsync(ollama, null, cancellationToken);
        if (!ready)
        {
            return new LocalModelStatus(
                true,
                false,
                false,
                Array.Empty<string>(),
                "Ollama is installed, but the local service is not responding yet. SoplyraAI will retry before using the model.");
        }

        var models = await TryGetInstalledModelsAsync(cancellationToken) ?? Array.Empty<string>();
        var installed = ContainsModel(models, model);
        return new LocalModelStatus(
            true,
            true,
            installed,
            models,
            installed
                ? $"{model} is already installed on this PC and ready to use."
                : $"{model} is not installed yet. Download it to use this local AI model.");
    }

    private static string? ResolveTrustedModel(string? modelName) =>
        AiProviderCatalog.LocalModels.FirstOrDefault(x => x.Equals(modelName, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsModel(IEnumerable<string> installedModels, string model) =>
        installedModels.Any(item => item.Equals(model, StringComparison.OrdinalIgnoreCase));

    private static async Task<bool> EnsureServerReadyAsync(
        string ollama,
        Action<string>? log,
        CancellationToken cancellationToken = default)
    {
        if (await IsApiReadyAsync(cancellationToken)) return true;

        log?.Invoke("Starting Ollama local service…");
        try
        {
            var psi = new ProcessStartInfo(ollama)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(ollama)!
            };
            psi.ArgumentList.Add("serve");
            using var process = Process.Start(psi);
        }
        catch (Exception ex)
        {
            log?.Invoke("Could not start Ollama service: " + PrivacySanitizer.Clean(ex.Message, 240));
        }

        for (var i = 0; i < 30; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(500, cancellationToken);
            if (await IsApiReadyAsync(cancellationToken)) return true;
        }

        return false;
    }

    private static async Task<bool> IsApiReadyAsync(CancellationToken cancellationToken = default) =>
        await TryGetInstalledModelsAsync(cancellationToken) is not null;

    private static async Task<IReadOnlyList<string>?> TryGetInstalledModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                UseDefaultCredentials = false,
                UseProxy = false
            };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
            using var response = await http.GetAsync(TagsEndpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();

            var names = new List<string>();
            foreach (var item in models.EnumerateArray())
            {
                string? name = null;
                if (item.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
                    name = nameElement.GetString();
                else if (item.TryGetProperty("model", out var modelElement) && modelElement.ValueKind == JsonValueKind.String)
                    name = modelElement.GetString();

                var clean = PrivacySanitizer.Clean(name, 160);
                if (!string.IsNullOrWhiteSpace(clean)) names.Add(clean);
            }

            return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch
        {
            return null;
        }
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

            // Ollama refreshes pull progress with carriage returns rather than normal lines.
            // Pump both streams character-by-character so the UI receives live percentage updates.
            var outputTask = PumpProgressAsync(process.StandardOutput, log);
            var errorTask = PumpProgressAsync(process.StandardError, log);

            using var cts = new CancellationTokenSource(timeout);
            try
            {
                await process.WaitForExitAsync(cts.Token);
                await Task.WhenAll(outputTask, errorTask);
                return process.ExitCode;
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                try { await Task.WhenAll(outputTask, errorTask); } catch { }
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

    private static async Task PumpProgressAsync(StreamReader reader, Action<string>? log)
    {
        var buffer = new char[256];
        var current = new StringBuilder(512);

        void Flush()
        {
            if (current.Length == 0) return;
            var text = PrivacySanitizer.Clean(current.ToString(), 500).Trim();
            current.Clear();
            if (!string.IsNullOrWhiteSpace(text)) log?.Invoke(text);
        }

        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length));
            if (read == 0) break;

            for (var i = 0; i < read; i++)
            {
                var ch = buffer[i];
                if (ch is '\r' or '\n')
                {
                    Flush();
                    continue;
                }

                if (current.Length < 1000) current.Append(ch);
            }
        }

        Flush();
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
