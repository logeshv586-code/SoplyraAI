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
        EndpointBox.Text = settings.AiEndpoint;
        ModelBox.Text = settings.AiModel;
        ApiKeyBox.Password = settings.AiApiKey;
        DelayBox.Text = settings.CaptureDelayMs.ToString();
        ScreenshotModeBox.SelectedIndex = settings.ScreenshotMode.Equals("FullDesktop", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private async void SetupButton_Click(object sender, RoutedEventArgs e)
    {
        SetupButton.IsEnabled = false;
        SetupStatus.Text = "Setting up local AI…";
        var lines = new List<string>();
        var result = await _setup.SetupAsync(line => Dispatcher.Invoke(() =>
        {
            lines.Add(line);
            SetupStatus.Text = line;
        }));
        SetupStatus.Text = result;
        if (result.StartsWith("Local AI is ready", StringComparison.OrdinalIgnoreCase))
        {
            UseAiCheck.IsChecked = true;
            EndpointBox.Text = "http://127.0.0.1:11434/v1";
            ModelBox.Text = "qwen2.5:0.5b";
            ApiKeyBox.Password = "ollama";
        }
        SetupButton.IsEnabled = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.UseLocalAi = UseAiCheck.IsChecked == true;
        _settings.AiEndpoint = EndpointBox.Text.Trim();
        _settings.AiModel = ModelBox.Text.Trim();
        _settings.AiApiKey = ApiKeyBox.Password;
        _settings.ScreenshotMode = (ScreenshotModeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ActiveWindow";
        _settings.CaptureDelayMs = int.TryParse(DelayBox.Text, out var ms) ? Math.Clamp(ms, 0, 1000) : 180;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
