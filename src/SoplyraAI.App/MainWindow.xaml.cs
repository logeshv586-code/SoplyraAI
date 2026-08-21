using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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
        var existing = _sessions.LoadAll();
        _current = existing.FirstOrDefault() ?? _sessions.Create("Untitled guide");
        _recorder = NewRecorder();

        Loaded += (_, _) =>
        {
            RefreshSessions(selectCurrent: true);
            UpdateEngineSummary();
            ApplyExportSelection();
            if (!_settings.HasCompletedAiSetup)
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => OpenSettings(firstRun: true)));
        };
        Closing += (_, _) =>
        {
            if (_recorder.IsRecording) _recorder.Stop();
            _recorder.Dispose();
            _sessions.Save(_current);
        };
        BindCurrent();
    }

    private RecorderService NewRecorder()
    {
        var recorder = new RecorderService(_sessions, _settings);
        recorder.StepCaptured += (_, step) =>
        {
            var session = _current;
            Dispatcher.Invoke(() =>
            {
                StepsList.ItemsSource = session.Steps;
                SetHasSteps(true);
                _captureBar?.SetStepCount(session.Steps.Count);
            });
            _ = ImproveCapturedStepAsync(session, step);
        };
        return recorder;
    }

    private async Task ImproveCapturedStepAsync(GuideSession session, GuideStep step)
    {
        if (!_settings.HasCompletedAiSetup) return;
        var improved = await _describer.ImproveAsync(step, session, _settings);
        if (string.IsNullOrWhiteSpace(improved)) return;
        await Dispatcher.InvokeAsync(() =>
        {
            step.Description = improved;
            if (session.Id == _current.Id) StepsList.Items.Refresh();
            _sessions.Save(session);
        });
    }

    private void BindCurrent()
    {
        GuideTitle.Text = _current.Title;
        StepsList.ItemsSource = _current.Steps;
        ModeStatusText.Text = _current.DocumentationMode == "Detailed" ? "Detailed SOP" : "Quick visual guide";
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
        if (selectCurrent) SessionList.SelectedItem = _recent.FirstOrDefault(s => s.Id == _current.Id);
    }

    private void UpdateEngineSummary()
    {
        var provider = AiProviderCatalog.Get(_settings.AiProvider);
        EngineNameText.Text = provider.DisplayName;
        EngineModelText.Text = _settings.AiModel;
        EngineVisionText.Text = _settings.SendScreenshotsToAi && AiProviderCatalog.IsVisionModel(provider.Id, _settings.AiModel)
            ? (provider.Id == "Ollama" ? "Local screenshot vision enabled" : "Screenshot vision explicitly enabled")
            : "Metadata-only AI";
    }

    private void ApplyExportSelection()
    {
        ExportFormatBox.SelectedIndex = _settings.DefaultExportFormat switch { "Word" => 1, "HTML" => 2, "All" => 3, _ => 0 };
    }

    private void NewGuide_Click(object sender, RoutedEventArgs e)
    {
        if (_recorder.IsRecording) StopRecording();
        _sessions.Save(_current);
        _current = _sessions.Create($"New guide {DateTime.Now:dd MMM HH:mm}");
        _current.DocumentationMode = _settings.DocumentationMode;
        _sessions.Save(_current);
        BindCurrent();
        RefreshSessions(selectCurrent: true);
    }

    private void StartCapture_Click(object sender, RoutedEventArgs e)
    {
        if (_recorder.IsRecording) return;
        if (!_settings.HasCompletedAiSetup) OpenSettings(firstRun: true);

        var modeWindow = new CaptureModeWindow(_current.DocumentationMode) { Owner = this };
        if (modeWindow.ShowDialog() != true) return;

        _settings.DocumentationMode = modeWindow.SelectedMode;
        _current.DocumentationMode = modeWindow.SelectedMode;
        _settingsStore.Save(_settings);
        ModeStatusText.Text = modeWindow.SelectedMode == "Detailed" ? "Detailed SOP" : "Quick visual guide";

        _current.Title = PrivacySanitizer.Clean(GuideTitle.Text, 200);
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
        if (_current.Steps.Count == 0)
        {
            MessageBox.Show("Capture at least one step first.", "SoplyraAI");
            return;
        }
        if (!_settings.HasCompletedAiSetup)
        {
            OpenSettings(firstRun: true);
            if (!_settings.HasCompletedAiSetup) return;
        }

        ImproveButton.IsEnabled = false;
        ImproveButton.Content = "Improving…";
        var improved = 0;
        try
        {
            foreach (var step in _current.Steps)
            {
                var text = await _describer.ImproveAsync(step, _current, _settings);
                if (!string.IsNullOrWhiteSpace(text)) { step.Description = text; improved++; }
            }
            _sessions.Save(_current);
            StepsList.Items.Refresh();
        }
        finally
        {
            ImproveButton.Content = "✦  Improve with AI";
            ImproveButton.IsEnabled = true;
        }

        MessageBox.Show(improved > 0 ? $"Improved {improved} steps using {_settings.AiProvider}." : "The configured AI could not improve the steps. Existing descriptions were kept.", "SoplyraAI");
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_current.Steps.Count == 0)
        {
            MessageBox.Show("Capture at least one step before exporting.", "SoplyraAI");
            return;
        }

        try
        {
            _current.Title = PrivacySanitizer.Clean(GuideTitle.Text, 200);
            if (string.IsNullOrWhiteSpace(_current.Title)) _current.Title = "Untitled guide";
            _sessions.Save(_current);

            var format = (ExportFormatBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PDF";
            _settings.DefaultExportFormat = format;
            _settingsStore.Save(_settings);
            var folder = ExportService.NewExportFolder(_current);
            var outputs = new List<string>();

            switch (format)
            {
                case "HTML": outputs.Add(_exporter.ExportHtml(_current, folder)); break;
                case "Word": outputs.Add(_exporter.ExportDocx(_current, folder)); break;
                case "All":
                    outputs.Add(_exporter.ExportHtml(_current, folder));
                    outputs.Add(_exporter.ExportDocx(_current, folder));
                    outputs.Add(_exporter.ExportMarkdown(_current, folder));
                    var allPdf = await _exporter.ExportPdfAsync(_current, folder);
                    if (allPdf is not null) outputs.Add(allPdf);
                    break;
                default:
                    var pdf = await _exporter.ExportPdfAsync(_current, folder);
                    if (pdf is null) throw new InvalidOperationException("PDF export needs Microsoft Edge or Google Chrome. HTML and Word export are still available.");
                    outputs.Add(pdf);
                    break;
            }

            var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            if (File.Exists(explorer))
            {
                var psi = new ProcessStartInfo { FileName = explorer, UseShellExecute = false };
                psi.ArgumentList.Add(folder);
                Process.Start(psi);
            }
            MessageBox.Show($"Export completed: {outputs.Count} file(s).\n\n{folder}", "SoplyraAI");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export could not be completed: {PrivacySanitizer.Clean(ex.Message, 400)}", "SoplyraAI", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings(firstRun: false);

    private void OpenSettings(bool firstRun)
    {
        var window = new SettingsWindow(_settings, _aiSetup, firstRun) { Owner = this };
        if (window.ShowDialog() == true)
        {
            _settingsStore.Save(_settings);
            UpdateEngineSummary();
            ApplyExportSelection();
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

    private void DeleteGuide_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: GuideSession session }) return;
        var confirm = MessageBox.Show($"Delete “{session.Title}” and its local screenshots?\n\nThis cannot be undone.", "Delete guide", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        if (session.Id == _current.Id && _recorder.IsRecording) StopRecording();
        if (!_sessions.Delete(session.Id))
        {
            MessageBox.Show("The guide could not be deleted safely.", "SoplyraAI", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (session.Id == _current.Id)
            _current = _sessions.LoadAll().FirstOrDefault() ?? _sessions.Create("Untitled guide");
        BindCurrent();
        RefreshSessions(selectCurrent: true);
    }

    private void Renumber()
    {
        for (var i = 0; i < _current.Steps.Count; i++) _current.Steps[i].Number = i + 1;
        StepsList.Items.Refresh();
    }

    private void GuideTitle_LostFocus(object sender, RoutedEventArgs e)
    {
        _current.Title = PrivacySanitizer.Clean(GuideTitle.Text, 200);
        if (string.IsNullOrWhiteSpace(_current.Title)) _current.Title = "Untitled guide";
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
