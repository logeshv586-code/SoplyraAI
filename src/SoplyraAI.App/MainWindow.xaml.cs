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
    private readonly ReliablePdfExportService _pdfExporter;
    private readonly LocalAiSetupService _aiSetup = new();
    private AppSettings _settings;
    private RecorderService _recorder;
    private GuideSession _current;
    private CaptureBarWindow? _captureBar;
    private ObservableCollection<GuideSession> _recent = new();

    public MainWindow()
    {
        InitializeComponent();
        _pdfExporter = new ReliablePdfExportService(_exporter);
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
        ExportFormatBox.SelectedIndex = _settings.DefaultExportFormat switch
        {
            "PDF" => 1,
            "Word" => 2,
            "HTML" => 3,
            "Markdown" => 4,
            _ => 0
        };
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
                if (!string.IsNullOrWhiteSpace(text))
                {
                    step.Description = text;
                    improved++;
                }
            }
            _sessions.Save(_current);
            StepsList.Items.Refresh();
        }
        finally
        {
            ImproveButton.Content = "✦  Improve with AI";
            ImproveButton.IsEnabled = true;
        }

        MessageBox.Show(
            improved > 0
                ? $"Improved {improved} steps using {_settings.AiProvider}."
                : "The configured AI could not improve the steps. Existing descriptions were kept.",
            "SoplyraAI");
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_current.Steps.Count == 0)
        {
            MessageBox.Show("Capture at least one step before exporting.", "SoplyraAI");
            return;
        }

        _current.Title = PrivacySanitizer.Clean(GuideTitle.Text, 200);
        if (string.IsNullOrWhiteSpace(_current.Title)) _current.Title = "Untitled guide";
        _sessions.Save(_current);

        var format = (ExportFormatBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PDF";
        await PerformExportAsync(_current, format);
    }

    private async void ExportSidebarGuide_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: GuideSession session }) return;
        if (session.Steps.Count == 0)
        {
            MessageBox.Show("This guide does not contain any recorded steps to export.", "SoplyraAI");
            return;
        }

        var format = (ExportFormatBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PDF";
        await PerformExportAsync(session, format);
    }

    private async Task PerformExportAsync(GuideSession session, string formatOption)
    {
        if (session.Steps.Count == 0)
        {
            MessageBox.Show("This guide contains 0 steps. Capture at least one step before exporting.", "SoplyraAI");
            return;
        }

        try
        {
            var folder = ExportService.NewExportFolder(session);
            var outputs = new List<string>();
            var warnings = new List<string>();

            if (formatOption.Contains("PDF") && !formatOption.Contains("All"))
            {
                var pdf = await _pdfExporter.ExportAsync(session, folder);
                if (pdf is null)
                    throw new InvalidOperationException("PDF generation did not produce a file. Word and HTML export remain available.");
                outputs.Add(pdf);
            }
            else if (formatOption.Contains("Word") || formatOption.Contains("docx"))
            {
                outputs.Add(_exporter.ExportDocx(session, folder));
            }
            else if (formatOption.Contains("HTML") || formatOption.Contains("Web"))
            {
                outputs.Add(_exporter.ExportHtml(session, folder));
            }
            else if (formatOption.Contains("Markdown") || formatOption.Contains("MD"))
            {
                outputs.Add(_exporter.ExportMarkdown(session, folder));
            }
            else
            {
                outputs.Add(_exporter.ExportHtml(session, folder));
                outputs.Add(_exporter.ExportDocx(session, folder));
                outputs.Add(_exporter.ExportMarkdown(session, folder));

                var pdf = await _pdfExporter.ExportAsync(session, folder);
                if (pdf is not null)
                    outputs.Add(pdf);
                else
                    warnings.Add("PDF could not be generated; Word, HTML and Markdown were generated successfully.");
            }

            if (outputs.Count == 0)
                throw new InvalidOperationException("No export file was generated.");

            var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            if (File.Exists(explorer))
            {
                var psi = new ProcessStartInfo { FileName = explorer, UseShellExecute = false };
                psi.ArgumentList.Add(folder);
                Process.Start(psi);
            }

            var fileList = string.Join("\n• ", outputs.Select(Path.GetFileName));
            var warningText = warnings.Count == 0 ? "" : "\n\nNotice:\n" + string.Join("\n", warnings);
            MessageBox.Show(
                $"Export successfully generated for:\n“{session.Title}”\n\nExported Files:\n• {fileList}{warningText}\n\nFolder:\n{folder}",
                "SoplyraAI Export Complete",
                MessageBoxButton.OK,
                warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Export could not be completed: {PrivacySanitizer.Clean(ex.Message, 500)}",
                "SoplyraAI Export",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
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
        e.Handled = true;
        var element = sender as FrameworkElement;
        var step = element?.DataContext as GuideStep ?? (sender as Button)?.Tag as GuideStep;
        if (step is null) return;

        try
        {
            if (!_sessions.DeleteStep(_current, step.Id))
            {
                MessageBox.Show(
                    "This captured step could not be found in the active guide. Reload the guide and try again.",
                    "SoplyraAI",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // ObservableCollection notifies the UI immediately. Rebinding as well makes the Remove action
            // deterministic even for guides that were loaded/saved by an older SoplyraAI build.
            StepsList.ItemsSource = null;
            StepsList.ItemsSource = _current.Steps;
            StepsList.Items.Refresh();
            SetHasSteps(_current.Steps.Count > 0);
            _captureBar?.SetStepCount(_current.Steps.Count);
            RefreshSessions(selectCurrent: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The captured step could not be removed safely: {PrivacySanitizer.Clean(ex.Message, 300)}",
                "SoplyraAI",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void RenameGuide_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var session = (sender as FrameworkElement)?.DataContext as GuideSession ?? (sender as Button)?.Tag as GuideSession;
        if (session is null) return;

        if (_recorder.IsRecording) StopRecording();
        if (session.Id != _current.Id)
        {
            _sessions.Save(_current);
            _current = session;
            BindCurrent();
        }

        SessionList.SelectedItem = SessionList.Items.Cast<object>()
            .OfType<GuideSession>()
            .FirstOrDefault(item => item.Id == _current.Id);
        GuideTitle.Focus();
        GuideTitle.SelectAll();
    }

    private void DeleteGuide_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: GuideSession session }) return;
        var confirm = MessageBox.Show(
            $"Delete “{session.Title}” and its local screenshots?\n\nThis cannot be undone.",
            "Delete guide",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
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
