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
        HeaderTitle.Text = firstRun ? "Welcome — choose your AI engine" : "AI & capture settings";

        LocalModelBox.ItemsSource = AiProviderCatalog.LocalModels;
        ProviderBox.ItemsSource = AiProviderCatalog.Cloud.Select(x => x.DisplayName).ToArray();
        var selected = AiProviderCatalog.Get(settings.AiProvider);
        LocalRadio.IsChecked = AiProviderCatalog.IsLocal(selected.Id);
        CloudRadio.IsChecked = !AiProviderCatalog.IsLocal(selected.Id);
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
        var local = LocalRadio.IsChecked == true;
        LocalPanel.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        CloudPanel.Visibility = local ? Visibility.Collapsed : Visibility.Visible;
        if (!local) RefreshCloudProvider();
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
        SetupStatus.Text = result;
        SetupButton.IsEnabled = true;
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        var candidate = BuildCandidate();
        if (!ValidateCandidate(candidate)) return;
        SetupStatus.Text = "Testing AI connection…";
        SetupStatus.Text = await _describer.TestConnectionAsync(candidate);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var candidate = BuildCandidate();
        if (!ValidateCandidate(candidate)) return;
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
        DialogResult = true;
    }

    private AppSettings BuildCandidate()
    {
        var local = LocalRadio.IsChecked == true;
        var provider = local ? AiProviderCatalog.Get("Ollama") : AiProviderCatalog.Cloud[Math.Clamp(ProviderBox.SelectedIndex, 0, AiProviderCatalog.Cloud.Count - 1)];
        var model = local ? LocalModelBox.SelectedItem?.ToString() ?? provider.DefaultModel : PrivacySanitizer.Clean(ModelBox.Text, 120);
        return new AppSettings
        {
            UseLocalAi = local,
            AllowRemoteAi = !local,
            SendScreenshotsToAi = local ? AiProviderCatalog.IsVisionModel(provider.Id, model) : VisionCheck.IsChecked == true && AiProviderCatalog.IsVisionModel(provider.Id, model),
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
        if (!AiEndpointPolicy.TryValidate(candidate.AiEndpoint, candidate.AllowRemoteAi, out _, out var endpointError))
        {
            MessageBox.Show(endpointError, "SoplyraAI security", MessageBoxButton.OK, MessageBoxImage.Warning); return false;
        }
        if (string.IsNullOrWhiteSpace(candidate.AiModel))
        {
            MessageBox.Show("Choose or enter a model name.", "SoplyraAI", MessageBoxButton.OK, MessageBoxImage.Warning); return false;
        }
        if (!candidate.UseLocalAi && string.IsNullOrWhiteSpace(candidate.AiApiKey))
        {
            MessageBox.Show("Enter the API key for the selected cloud provider.", "SoplyraAI", MessageBoxButton.OK, MessageBoxImage.Warning); return false;
        }
        if (candidate.AiApiKey.Length > 8192)
        {
            MessageBox.Show("The API key is too long.", "SoplyraAI security", MessageBoxButton.OK, MessageBoxImage.Warning); return false;
        }
        return true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_firstRun && !_settings.HasCompletedAiSetup) _settings.HasCompletedAiSetup = false;
        DialogResult = false;
    }
}
