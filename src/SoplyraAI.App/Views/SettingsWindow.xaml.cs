using System.Windows;
using System.Windows.Controls;
using SoplyraAI.Models;
using SoplyraAI.Services;

namespace SoplyraAI.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly LocalAiSetupService _setup;
    private readonly DescriptionService _describer = new();
    private readonly bool _firstRun;

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
    }

    private void EngineMode_Changed(object sender, RoutedEventArgs e) { if (IsLoaded) ApplyEngineMode(); }

    private void ApplyEngineMode()
    {
        var noAi = NoAiRadio.IsChecked == true;
        var local = !noAi && LocalRadio.IsChecked == true;
        var cloud = !noAi && CloudRadio.IsChecked == true;
        LocalPanel.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        CloudPanel.Visibility = cloud ? Visibility.Visible : Visibility.Collapsed;
        BuiltInPanel.Visibility = noAi ? Visibility.Visible : Visibility.Collapsed;
        if (cloud) RefreshCloudProvider();
    }

    private void ProviderBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (IsLoaded) RefreshCloudProvider(); }

    private void RefreshCloudProvider()
    {
        var options = AiProviderCatalog.Cloud;
        var index = Math.Clamp(ProviderBox.SelectedIndex, 0, options.Count - 1);
        var provider = options[index];
        ModelBox.ItemsSource = provider.Models;
        if (string.IsNullOrWhiteSpace(ModelBox.Text) || !provider.Models.Contains(ModelBox.Text)) ModelBox.Text = provider.DefaultModel;
        ProviderNote.Text = provider.Note;
        VisionCheck.IsEnabled = provider.SupportsVision;
        if (!provider.SupportsVision) VisionCheck.IsChecked = false;
    }

    private async void SetupButton_Click(object sender, RoutedEventArgs e)
    {
        SetupButton.IsEnabled = false;
        SetupStatus.Text = "Preparing local AI…";
        var model = LocalModelBox.SelectedItem?.ToString() ?? AiProviderCatalog.Get("Ollama").DefaultModel;
        var result = await _setup.SetupAsync(model, line => Dispatcher.Invoke(() => SetupStatus.Text = line));

        if (!result.StartsWith("Local AI is ready:", StringComparison.OrdinalIgnoreCase))
        {
            SetupStatus.Text = result;
            SetupButton.IsEnabled = true;
            return;
        }

        var candidate = BuildCandidate();
        SetupStatus.Text = $"{result}. Verifying the model before capture…";
        var connection = await _describer.TestConnectionAsync(candidate);
        if (!connection.StartsWith("Connected to ", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Delay(1200);
            connection = await _describer.TestConnectionAsync(candidate);
        }

        if (!connection.StartsWith("Connected to ", StringComparison.OrdinalIgnoreCase))
        {
            SetupStatus.Text = $"{result}\n{connection}\nThe model is downloaded, but SoplyraAI could not verify it yet. Use Test connection or restart Ollama and retry.";
            SetupButton.IsEnabled = true;
            return;
        }

        ApplyCandidate(candidate);
        SetupStatus.Text = $"{connection} The downloaded model is active for new captures.";
        SetupButton.IsEnabled = true;
        DialogResult = true;
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
        SetupStatus.Text = "Testing AI connection…";
        SetupStatus.Text = await _describer.TestConnectionAsync(candidate);
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
}
