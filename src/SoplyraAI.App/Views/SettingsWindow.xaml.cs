using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SoplyraAI.Models;
using SoplyraAI.Services;

namespace SoplyraAI.Views;

public partial class SettingsWindow : Window
{
    private static readonly Regex PercentPattern = new(
        @"(?<!\d)(?<percent>\d{1,3})\s*%",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly AppSettings _settings;
    private readonly LocalAiSetupService _setup;
    private readonly DescriptionService _describer = new();
    private readonly bool _firstRun;
    private bool _selectedLocalModelInstalled;
    private int _localStatusRequest;

    public SettingsWindow(AppSettings settings, LocalAiSetupService setup, bool firstRun = false)
    {
        InitializeComponent();
        _settings = settings;
        _setup = setup;
        _firstRun = firstRun;
        HeaderTitle.Text = firstRun ? "Welcome — choose how SoplyraAI writes steps" : "AI & capture settings";

        LocalModelBox.ItemsSource = AiProviderCatalog.LocalModels;
        ProviderBox.ItemsSource = AiProviderCatalog.Cloud.Select(x => x.DisplayName).ToArray();
        var selected = AiProviderCatalog.Get(settings.AiProvider);
        NoAiRadio.IsChecked = !settings.EnableAi;
        LocalRadio.IsChecked = settings.EnableAi && AiProviderCatalog.IsLocal(selected.Id);
        CloudRadio.IsChecked = settings.EnableAi && !AiProviderCatalog.IsLocal(selected.Id);
        LocalModelBox.SelectedItem = AiProviderCatalog.LocalModels.Contains(settings.AiModel) ? settings.AiModel : AiProviderCatalog.Get("Ollama").DefaultModel;
        var cloudIndex = AiProviderCatalog.Cloud.ToList().FindIndex(x => x.Id.Equals(selected.Id, StringComparison.OrdinalIgnoreCase));
        ProviderBox.SelectedIndex = cloudIndex >= 0 ? cloudIndex : 0;
        ModelBox.Text = settings.AiModel;
        ApiKeyBox.Password = settings.AiApiKey;
        VisionCheck.IsChecked = settings.SendScreenshotsToAi;
        DelayBox.Text = settings.CaptureDelayMs.ToString();
        ScreenshotModeBox.SelectedIndex = settings.ScreenshotMode.Equals("FullDesktop", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        ExportFormatBox.SelectedIndex = settings.DefaultExportFormat switch { "Word" => 1, "HTML" => 2, "All" => 3, _ => 0 };
        ApplyEngineMode();

        Loaded += async (_, _) =>
        {
            if (LocalRadio.IsChecked == true)
                await RefreshLocalModelStatusAsync();
        };
    }

    private void EngineMode_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) ApplyEngineMode();
    }

    private void ApplyEngineMode()
    {
        var noAi = NoAiRadio.IsChecked == true;
        var local = !noAi && LocalRadio.IsChecked == true;
        var cloud = !noAi && CloudRadio.IsChecked == true;
        LocalPanel.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        CloudPanel.Visibility = cloud ? Visibility.Visible : Visibility.Collapsed;
        BuiltInPanel.Visibility = noAi ? Visibility.Visible : Visibility.Collapsed;
        if (!local) SetupProgressPanel.Visibility = Visibility.Collapsed;
        if (local && IsLoaded) _ = RefreshLocalModelStatusAsync();
        if (cloud) RefreshCloudProvider();
    }

    private async void LocalModelBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && LocalRadio.IsChecked == true)
            await RefreshLocalModelStatusAsync();
    }

    private async Task RefreshLocalModelStatusAsync()
    {
        if (!IsLoaded || LocalRadio.IsChecked != true) return;

        var request = ++_localStatusRequest;
        var model = LocalModelBox.SelectedItem?.ToString() ?? AiProviderCatalog.Get("Ollama").DefaultModel;
        _selectedLocalModelInstalled = false;
        SetupButton.Content = "Download & use model";
        LocalModelStatusText.Text = $"Checking whether {model} is already installed…";
        AdvancedModelHintPanel.Visibility = Visibility.Collapsed;
        SetStateCard(ModelStateCard, ModelStateText, "Checking…", StateKind.Neutral);
        SetStateCard(ServiceStateCard, ServiceStateText, "Checking…", StateKind.Neutral);
        SetAiState("Not tested", StateKind.Neutral);

        LocalModelStatus status;
        try
        {
            status = await _setup.GetStatusAsync(model);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (request != _localStatusRequest || LocalRadio.IsChecked != true) return;

        LocalModelStatusText.Text = status.Message;
        _selectedLocalModelInstalled = status.ModelInstalled;
        SetupButton.Content = status.ModelInstalled ? "Use installed model" : "Download & use model";
        UpdateLocalStateCards(status);

        if (status.ModelInstalled)
        {
            AdvancedModelHintText.Text = BuildAdvancedModelHint(model, status.InstalledModels);
            AdvancedModelHintPanel.Visibility = Visibility.Visible;
        }
    }

    private void UpdateLocalStateCards(LocalModelStatus status)
    {
        SetStateCard(
            ModelStateCard,
            ModelStateText,
            status.ModelInstalled ? "✓ Installed" : "Not installed",
            status.ModelInstalled ? StateKind.Good : StateKind.Warning);

        var serviceText = status.ServiceReady
            ? "✓ Ollama running"
            : status.OllamaInstalled
                ? "Needs startup"
                : "Not installed";
        SetStateCard(
            ServiceStateCard,
            ServiceStateText,
            serviceText,
            status.ServiceReady ? StateKind.Good : StateKind.Warning);
    }

    private void SetAiState(string text, StateKind kind) =>
        SetStateCard(AiStateCard, AiStateText, text, kind);

    private static void SetStateCard(Border card, TextBlock textBlock, string text, StateKind kind)
    {
        var (background, border, foreground) = kind switch
        {
            StateKind.Good => (Color.FromRgb(236, 253, 245), Color.FromRgb(167, 243, 208), Color.FromRgb(4, 120, 87)),
            StateKind.Warning => (Color.FromRgb(255, 251, 235), Color.FromRgb(253, 230, 138), Color.FromRgb(180, 83, 9)),
            StateKind.Bad => (Color.FromRgb(254, 242, 242), Color.FromRgb(254, 202, 202), Color.FromRgb(185, 28, 28)),
            _ => (Color.FromRgb(248, 250, 252), Color.FromRgb(226, 232, 240), Color.FromRgb(71, 85, 105))
        };

        card.Background = new SolidColorBrush(background);
        card.BorderBrush = new SolidColorBrush(border);
        textBlock.Foreground = new SolidColorBrush(foreground);
        textBlock.Text = text;
    }

    private static string BuildAdvancedModelHint(string selectedModel, IReadOnlyList<string> installedModels)
    {
        string[] preference = selectedModel.ToLowerInvariant() switch
        {
            "qwen3:4b" => new[] { "qwen2.5vl:3b", "gemma3:4b", "deepseek-r1:7b" },
            "qwen2.5vl:3b" => new[] { "gemma3:4b", "deepseek-r1:7b", "qwen3:4b" },
            "deepseek-r1:7b" => new[] { "qwen2.5vl:3b", "gemma3:4b", "qwen3:4b" },
            "gemma3:4b" => new[] { "qwen2.5vl:3b", "deepseek-r1:7b", "qwen3:4b" },
            _ => AiProviderCatalog.LocalModels.Where(x => !x.Equals(selectedModel, StringComparison.OrdinalIgnoreCase)).ToArray()
        };

        var next = preference.FirstOrDefault(candidate =>
            !installedModels.Any(item => item.Equals(candidate, StringComparison.OrdinalIgnoreCase)));

        if (next is null)
            return "All recommended SoplyraAI local models are already installed. You can switch between them without downloading again.";

        var benefit = next.ToLowerInvariant() switch
        {
            "qwen2.5vl:3b" => "adds screenshot vision for stronger understanding of what is visible on screen",
            "gemma3:4b" => "adds another multimodal option for richer screenshot-aware documentation",
            "deepseek-r1:7b" => "adds deeper reasoning for more complex workflow descriptions",
            _ => "adds another local documentation option"
        };

        return $"Advanced option: {next} {benefit}. Select it above and download it when you want more capability; {selectedModel} will remain installed.";
    }

    private void ProviderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) RefreshCloudProvider();
    }

    private void RefreshCloudProvider()
    {
        var options = AiProviderCatalog.Cloud;
        var index = Math.Clamp(ProviderBox.SelectedIndex, 0, options.Count - 1);
        var provider = options[index];
        ModelBox.ItemsSource = provider.Models;
        if (string.IsNullOrWhiteSpace(ModelBox.Text) || !provider.Models.Contains(ModelBox.Text))
            ModelBox.Text = provider.DefaultModel;
        ProviderNote.Text = provider.Note;
        VisionCheck.IsEnabled = provider.SupportsVision;
        if (!provider.SupportsVision) VisionCheck.IsChecked = false;
    }

    private async void SetupButton_Click(object sender, RoutedEventArgs e)
    {
        var model = LocalModelBox.SelectedItem?.ToString() ?? AiProviderCatalog.Get("Ollama").DefaultModel;
        var wasInstalled = _selectedLocalModelInstalled;
        SetLocalSetupBusy(true);
        SetAiState("Preparing…", StateKind.Neutral);
        ShowSetupProgress(
            phase: wasInstalled ? $"Preparing installed model · {model}" : $"Preparing {model}",
            percent: wasInstalled ? 100 : null,
            detail: wasInstalled
                ? "This model already exists on this PC. SoplyraAI is starting Ollama and preparing it for use — no redownload is required."
                : "Checking Ollama and local model requirements…",
            indeterminate: !wasInstalled);

        var result = await _setup.SetupAsync(
            model,
            line => Dispatcher.BeginInvoke(new Action(() => UpdateSetupProgressFromLog(model, line))));

        if (!result.StartsWith("Local AI is ready:", StringComparison.OrdinalIgnoreCase))
        {
            SetAiState("Not active", StateKind.Bad);
            ShowSetupProgress(
                phase: "Local model could not be prepared",
                percent: null,
                detail: result,
                indeterminate: false);
            SetupStatus.Text = result;
            SetLocalSetupBusy(false);
            await RefreshLocalModelStatusAsync();
            return;
        }

        var alreadyInstalled = result.Contains("already installed", StringComparison.OrdinalIgnoreCase) || wasInstalled;
        var candidate = BuildCandidate();
        ShowSetupProgress(
            phase: alreadyInstalled ? "Using installed model" : "Verifying downloaded model",
            percent: 100,
            detail: alreadyInstalled
                ? $"{model} is already stored locally. SoplyraAI is checking that the model can answer requests."
                : $"{model} is downloaded. SoplyraAI is checking the local AI connection…",
            indeterminate: false);

        var connection = await _describer.TestConnectionAsync(candidate);
        if (!connection.StartsWith("Connected to ", StringComparison.OrdinalIgnoreCase))
        {
            ShowSetupProgress(
                phase: "Starting local AI service",
                percent: 100,
                detail: "The model files are present. SoplyraAI is restarting/checking Ollama and allowing the model extra time to warm up.",
                indeterminate: false);

            _ = await _setup.GetStatusAsync(model);
            await Task.Delay(1800);
            connection = await _describer.TestConnectionAsync(candidate);
        }

        if (!connection.StartsWith("Connected to ", StringComparison.OrdinalIgnoreCase))
        {
            _selectedLocalModelInstalled = true;
            SetupButton.Content = "Use installed model";
            SetAiState("Connection retry", StateKind.Warning);
            ShowSetupProgress(
                phase: "Model installed · connection needs retry",
                percent: 100,
                detail: $"{model} is already downloaded, so do not download it again. Ollama did not answer the model test yet. Use ‘Test connection’ again after a few seconds or reopen SoplyraAI.",
                indeterminate: false);
            SetupStatus.Text = $"{model} is installed locally. {connection} No redownload is required.";
            SetLocalSetupBusy(false);
            await RefreshLocalModelStatusAsync();
            SetAiState("Connection retry", StateKind.Warning);
            return;
        }

        _selectedLocalModelInstalled = true;
        SetStateCard(ModelStateCard, ModelStateText, "✓ Installed", StateKind.Good);
        SetStateCard(ServiceStateCard, ServiceStateText, "✓ Ollama running", StateKind.Good);
        SetAiState("✓ Active", StateKind.Good);
        ShowSetupProgress(
            phase: alreadyInstalled ? "Installed model ready to use" : "Model ready to use",
            percent: 100,
            detail: $"{connection} SoplyraAI can now use {model} for new captured steps.",
            indeterminate: false);
        SetupStatus.Text = $"AI documentation active with {model}. New captures keep screenshots and UI context, then SoplyraAI uses this model to improve step wording. Click Save & continue to make this the selected engine.";
        SetLocalSetupBusy(false);
    }

    private void UpdateSetupProgressFromLog(string model, string line)
    {
        var clean = PrivacySanitizer.Clean(line, 500).Trim();
        if (string.IsNullOrWhiteSpace(clean)) return;

        var percent = ParsePercent(clean);
        var phase = $"Downloading {model}";

        if (clean.Contains("already installed", StringComparison.OrdinalIgnoreCase))
        {
            phase = "Model already installed";
            percent = 100;
            _selectedLocalModelInstalled = true;
            SetStateCard(ModelStateCard, ModelStateText, "✓ Installed", StateKind.Good);
        }
        else if (clean.Contains("starting ollama", StringComparison.OrdinalIgnoreCase))
            phase = "Starting Ollama local service";
        else if (clean.Contains("install", StringComparison.OrdinalIgnoreCase))
            phase = "Installing Ollama";
        else if (clean.Contains("manifest", StringComparison.OrdinalIgnoreCase))
            phase = $"Preparing {model}";
        else if (clean.Contains("verify", StringComparison.OrdinalIgnoreCase))
            phase = "Verifying model files";
        else if (clean.Contains("success", StringComparison.OrdinalIgnoreCase) || clean.Contains("download complete", StringComparison.OrdinalIgnoreCase))
            phase = "Finalizing local model";

        ShowSetupProgress(
            phase,
            percent,
            CleanProgressDetail(clean),
            indeterminate: percent is null);
    }

    private static int? ParsePercent(string text)
    {
        var match = PercentPattern.Match(text);
        if (!match.Success || !int.TryParse(match.Groups["percent"].Value, out var value))
            return null;
        return Math.Clamp(value, 0, 100);
    }

    private static string CleanProgressDetail(string text)
    {
        var cleaned = text.Replace("\r", " ").Replace("\n", " ").Trim();
        while (cleaned.Contains("  ", StringComparison.Ordinal))
            cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);
        return cleaned.Length <= 220 ? cleaned : cleaned[..217] + "…";
    }

    private void ShowSetupProgress(
        string phase,
        int? percent,
        string detail,
        bool? indeterminate = null)
    {
        SetupProgressPanel.Visibility = Visibility.Visible;
        SetupPhaseText.Text = phase;
        SetupDetailText.Text = detail;

        var useIndeterminate = indeterminate ?? percent is null;
        SetupProgressBar.IsIndeterminate = useIndeterminate;

        if (percent.HasValue)
        {
            SetupProgressBar.Value = percent.Value;
            SetupProgressPercent.Text = $"{percent.Value}%";
        }
        else
        {
            SetupProgressBar.Value = 0;
            SetupProgressPercent.Text = useIndeterminate ? "Working…" : "";
        }
    }

    private void SetLocalSetupBusy(bool busy)
    {
        SetupButton.IsEnabled = !busy;
        TestConnectionButton.IsEnabled = !busy;
        LocalModelBox.IsEnabled = !busy;
        SetupButton.Content = busy
            ? (_selectedLocalModelInstalled ? "Preparing model…" : "Downloading…")
            : (_selectedLocalModelInstalled ? "Use installed model" : "Download & use model");
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        var candidate = BuildCandidate();
        if (!candidate.EnableAi)
        {
            SetupStatus.Text = "Built-in wording is ready. No model or connection is required.";
            return;
        }
        if (!ValidateCandidate(candidate)) return;

        if (candidate.UseLocalAi)
        {
            SetupStatus.Text = "Checking installed local model and Ollama service…";
            var status = await _setup.GetStatusAsync(candidate.AiModel);
            LocalModelStatusText.Text = status.Message;
            _selectedLocalModelInstalled = status.ModelInstalled;
            SetupButton.Content = status.ModelInstalled ? "Use installed model" : "Download & use model";
            UpdateLocalStateCards(status);

            if (!status.OllamaInstalled)
            {
                SetAiState("Cannot test", StateKind.Bad);
                SetupStatus.Text = "Ollama is not installed yet. Use Download & use model first.";
                return;
            }
            if (!status.ServiceReady)
            {
                SetAiState("Service offline", StateKind.Warning);
                SetupStatus.Text = "Ollama is installed but its local service is not ready. Reopen SoplyraAI or use the model button to retry startup.";
                return;
            }
            if (!status.ModelInstalled)
            {
                SetAiState("Model missing", StateKind.Warning);
                SetupStatus.Text = $"{candidate.AiModel} is not installed yet. Download it before testing the model.";
                return;
            }
        }

        SetAiState("Testing…", StateKind.Neutral);
        SetupStatus.Text = "Testing AI connection and model generation…";
        var connection = await _describer.TestConnectionAsync(candidate);
        SetupStatus.Text = connection;

        if (connection.StartsWith("Connected to ", StringComparison.OrdinalIgnoreCase))
        {
            SetAiState("✓ Active", StateKind.Good);
            if (candidate.UseLocalAi)
            {
                SetupStatus.Text = $"{connection} AI documentation is active. When you start capture, every recorded step keeps its screenshot and UI context, then {candidate.AiModel} is asked to improve the documentation wording. Weak AI output is automatically replaced by SoplyraAI's grounded fallback.";
            }
        }
        else
        {
            SetAiState("Test failed", StateKind.Bad);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var candidate = BuildCandidate();
        if (!ValidateCandidate(candidate)) return;
        ApplyCandidate(candidate);
        DialogResult = true;
    }

    private AppSettings BuildCandidate()
    {
        var noAi = NoAiRadio.IsChecked == true;
        var local = !noAi && LocalRadio.IsChecked == true;

        if (noAi)
        {
            return new AppSettings
            {
                EnableAi = false,
                UseLocalAi = false,
                AllowRemoteAi = false,
                SendScreenshotsToAi = false,
                HasCompletedAiSetup = true,
                AiProvider = _settings.AiProvider,
                AiEndpoint = _settings.AiEndpoint,
                AiModel = _settings.AiModel,
                AiApiKey = _settings.AiApiKey,
                DocumentationMode = _settings.DocumentationMode,
                DefaultExportFormat = (ExportFormatBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PDF",
                ScreenshotMode = (ScreenshotModeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ActiveWindow",
                CaptureDelayMs = int.TryParse(DelayBox.Text, out var noAiMs) ? Math.Clamp(noAiMs, 0, 1000) : 180
            };
        }

        var provider = local
            ? AiProviderCatalog.Get("Ollama")
            : AiProviderCatalog.Cloud[Math.Clamp(ProviderBox.SelectedIndex, 0, AiProviderCatalog.Cloud.Count - 1)];
        var model = local
            ? LocalModelBox.SelectedItem?.ToString() ?? provider.DefaultModel
            : PrivacySanitizer.Clean(ModelBox.Text, 120);

        return new AppSettings
        {
            EnableAi = true,
            UseLocalAi = local,
            AllowRemoteAi = !local,
            SendScreenshotsToAi = local
                ? AiProviderCatalog.IsVisionModel(provider.Id, model)
                : VisionCheck.IsChecked == true && AiProviderCatalog.IsVisionModel(provider.Id, model),
            HasCompletedAiSetup = true,
            AiProvider = provider.Id,
            AiEndpoint = provider.Endpoint,
            AiModel = model,
            AiApiKey = local ? "" : ApiKeyBox.Password,
            DocumentationMode = _settings.DocumentationMode,
            DefaultExportFormat = (ExportFormatBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PDF",
            ScreenshotMode = (ScreenshotModeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ActiveWindow",
            CaptureDelayMs = int.TryParse(DelayBox.Text, out var ms) ? Math.Clamp(ms, 0, 1000) : 180
        };
    }

    private static bool ValidateCandidate(AppSettings candidate)
    {
        if (!candidate.EnableAi) return true;

        if (!AiEndpointPolicy.TryValidate(candidate.AiEndpoint, candidate.AllowRemoteAi, out _, out var endpointError))
        {
            MessageBox.Show(endpointError, "SoplyraAI security", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (string.IsNullOrWhiteSpace(candidate.AiModel))
        {
            MessageBox.Show("Choose or enter a model name.", "SoplyraAI", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (!candidate.UseLocalAi && string.IsNullOrWhiteSpace(candidate.AiApiKey))
        {
            MessageBox.Show("Enter the API key for the selected cloud provider.", "SoplyraAI", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (candidate.AiApiKey.Length > 8192)
        {
            MessageBox.Show("The API key is too long.", "SoplyraAI security", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    private void ApplyCandidate(AppSettings candidate)
    {
        _settings.EnableAi = candidate.EnableAi;
        _settings.UseLocalAi = candidate.UseLocalAi;
        _settings.AllowRemoteAi = candidate.AllowRemoteAi;
        _settings.SendScreenshotsToAi = candidate.SendScreenshotsToAi;
        _settings.HasCompletedAiSetup = true;
        _settings.AiProvider = candidate.AiProvider;
        _settings.AiEndpoint = candidate.AiEndpoint;
        _settings.AiModel = candidate.AiModel;
        _settings.AiApiKey = candidate.AiApiKey;
        _settings.ScreenshotMode = candidate.ScreenshotMode;
        _settings.CaptureDelayMs = candidate.CaptureDelayMs;
        _settings.DefaultExportFormat = candidate.DefaultExportFormat;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_firstRun && !_settings.HasCompletedAiSetup) _settings.HasCompletedAiSetup = false;
        DialogResult = false;
    }

    private enum StateKind
    {
        Neutral,
        Good,
        Warning,
        Bad
    }
}