using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SoplyraAI.Models;
using SoplyraAI.Services;
using SoplyraAI.Views;

namespace SoplyraAI;

public partial class MainWindow : Window
{
    private readonly SessionStore _sessions = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly DescriptionService _describer = new();
    private readonly ExportService _exporter = new();
    private readonly LocalAiSetupService _aiSetup = new();
    private AppSettings _settings;
    private RecorderService _recorder;
    private GuideSession _current;
    private CaptureBarWindow? _captureBar;
    private ObservableCollection<GuideSession> _recent = new();

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsStore.Load();
        _recorder = NewRecorder();
        _current = _sessions.Create("Untitled guide");
        Loaded += (_, _) => RefreshSessions(selectCurrent: true);
        Closing += (_, _) => { _recorder.Dispose(); _sessions.Save(_current); };
        BindCurrent();
    }

    private RecorderService NewRecorder()
    {
        var recorder = new RecorderService(_sessions, _settings);
        recorder.StepCaptured += (_, step) => Dispatcher.Invoke(() =>
        {
            StepsList.ItemsSource = _current.Steps;
            SetHasSteps(true);
            _captureBar?.SetStepCount(_current.Steps.Count);
        });
        return recorder;
    }

    private void BindCurrent()
    {
        GuideTitle.Text = _current.Title;
        StepsList.ItemsSource = _current.Steps;
        SetHasSteps(_current.Steps.Count > 0);
    }

    private void SetHasSteps(bool hasSteps)
    {
        EmptyState.Visibility = hasSteps ? Visibility.Collapsed : Visibility.Visible;
        StepsScroll.Visibility = hasSteps ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshSessions(bool selectCurrent = false)
    {
        _recent = new ObservableCollection<GuideSession>(_sessions.LoadAll());
        SessionList.ItemsSource = _recent;
        if (selectCurrent)
            SessionList.SelectedItem = _recent.FirstOrDefault(s => s.Id == _current.Id);
    }

    private void NewGuide_Click(object sender, RoutedEventArgs e)
    {
        if (_recorder.IsRecording) StopRecording();
        _sessions.Save(_current);
        _current = _sessions.Create($"New guide {DateTime.Now:dd MMM HH:mm}");
        BindCurrent();
        RefreshSessions(selectCurrent: true);
    }

    private void StartCapture_Click(object sender, RoutedEventArgs e)
    {
        if (_recorder.IsRecording) return;
        _current.Title = GuideTitle.Text.Trim();
        if (string.IsNullOrWhiteSpace(_current.Title)) _current.Title = "Untitled guide";
        _sessions.Save(_current);
        _recorder.Start(_current);

        _captureBar = new CaptureBarWindow();
        _captureBar.SetStepCount(_current.Steps.Count);
        _captureBar.PauseRequested += (_, _) => _recorder.TogglePause();
        _captureBar.StopRequested += (_, _) => StopRecording();
        _captureBar.Show();
        WindowState = WindowState.Minimized;
    }

    private void StopRecording()
    {
        _recorder.Stop();
        _captureBar?.Close();
        _captureBar = null;
        WindowState = WindowState.Normal;
        Activate();
        RefreshSessions(selectCurrent: true);
    }

    private async void ImproveAll_Click(object sender, RoutedEventArgs e)
    {
        if (_current.Steps.Count == 0) { MessageBox.Show("Capture at least one step first.", "SoplyraAI"); return; }
        ImproveButton.IsEnabled = false;
        ImproveButton.Content = "Improving…";
        int improved = 0;
        foreach (var step in _current.Steps)
        {
            var text = await _describer.ImproveAsync(step, _settings);
            if (!string.IsNullOrWhiteSpace(text)) { step.Description = text; improved++; }
        }
        _sessions.Save(_current);
        ImproveButton.Content = "✨ Improve with AI";
        ImproveButton.IsEnabled = true;
        MessageBox.Show(improved > 0 ? $"Improved {improved} steps using the configured local AI." : "Local AI was not reachable. Your fast deterministic descriptions were kept.", "SoplyraAI");
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_current.Steps.Count == 0) { MessageBox.Show("Capture at least one step before exporting.", "SoplyraAI"); return; }
        _current.Title = GuideTitle.Text.Trim();
        _sessions.Save(_current);
        var folder = ExportService.NewExportFolder(_current);
        _exporter.ExportHtml(_current, folder);
        _exporter.ExportMarkdown(_current, folder);
        await _exporter.ExportPdfAsync(_current, folder);
        Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_settings, _aiSetup) { Owner = this };
        if (window.ShowDialog() == true)
        {
            _settingsStore.Save(_settings);
        }
    }

    private void DeleteStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: GuideStep step })
        {
            _current.Steps.Remove(step);
            Renumber();
            _sessions.Save(_current);
            SetHasSteps(_current.Steps.Count > 0);
        }
    }

    private void Renumber()
    {
        for (var i = 0; i < _current.Steps.Count; i++) _current.Steps[i].Number = i + 1;
        StepsList.Items.Refresh();
    }

    private void GuideTitle_LostFocus(object sender, RoutedEventArgs e)
    {
        _current.Title = string.IsNullOrWhiteSpace(GuideTitle.Text) ? "Untitled guide" : GuideTitle.Text.Trim();
        _sessions.Save(_current);
        RefreshSessions(selectCurrent: true);
    }

    private void SessionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SessionList.SelectedItem is not GuideSession selected || selected.Id == _current.Id) return;
        if (_recorder.IsRecording) StopRecording();
        _sessions.Save(_current);
        _current = selected;
        BindCurrent();
    }
}
