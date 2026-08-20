using System.Windows;
using System.Windows.Controls;
using SoplyraAI.Models;
using SoplyraAI.Services;

namespace SoplyraAI.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly LocalAiSetupService _setup;

    public SettingsWindow(AppSettings settings, LocalAiSetupService setup)
    {
        InitializeComponent();
        _settings = settings;
        _setup = setup;

        UseAiCheck.IsChecked = settings.UseLocalAi;
        AllowRemoteAiCheck.IsChecked = settings.AllowRemoteAi;
        EndpointBox.Text = settings.AiEndpoint;
        ModelBox.Text = settings.AiModel;
        ApiKeyBox.Password = settings.AiApiKey;
        DelayBox.Text = settings.CaptureDelayMs.ToString();
        ScreenshotModeBox.SelectedIndex =
            settings.ScreenshotMode.Equals("FullDesktop", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private async void SetupButton_Click(object sender, RoutedEventArgs e)
    {
        SetupButton.IsEnabled = false;
        SetupStatus.Text = "Setting up local AI…";

        var result = await _setup.SetupAsync(line => Dispatcher.Invoke(() =>
        {
            SetupStatus.Text = line;
        }));

        SetupStatus.Text = result;

        if (result.StartsWith("Local AI is ready", StringComparison.OrdinalIgnoreCase))
        {
            UseAiCheck.IsChecked = true;
            AllowRemoteAiCheck.IsChecked = false;
            EndpointBox.Text = "http://127.0.0.1:11434/v1";
            ModelBox.Text = "qwen2.5:0.5b";
            ApiKeyBox.Password = "";
        }

        SetupButton.IsEnabled = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var useAi = UseAiCheck.IsChecked == true;
        var allowRemote = AllowRemoteAiCheck.IsChecked == true;
        var endpoint = EndpointBox.Text.Trim();

        if (useAi &&
            !AiEndpointPolicy.TryValidate(endpoint, allowRemote, out _, out var endpointError))
        {
            MessageBox.Show(endpointError, "SoplyraAI security", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var model = PrivacySanitizer.Clean(ModelBox.Text, 120);
        if (useAi && string.IsNullOrWhiteSpace(model))
        {
            MessageBox.Show("Enter a model name.", "SoplyraAI security", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ApiKeyBox.Password.Length > 8192)
        {
            MessageBox.Show("The API key is too long.", "SoplyraAI security", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.UseLocalAi = useAi;
        _settings.AllowRemoteAi = allowRemote;
        _settings.AiEndpoint = endpoint;
        _settings.AiModel = model;
        _settings.AiApiKey = ApiKeyBox.Password;
        _settings.ScreenshotMode =
            (ScreenshotModeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ActiveWindow";
        _settings.CaptureDelayMs =
            int.TryParse(DelayBox.Text, out var ms) ? Math.Clamp(ms, 0, 1000) : 180;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
