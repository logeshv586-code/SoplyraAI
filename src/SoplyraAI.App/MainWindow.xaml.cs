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
        GuideTitle.TextChanged += GuideTitle_TextChanged;
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
            SyncCurrentTitleFromEditor();
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

        var modelOutput = await _describer.ImproveAsync(step, session, _settings);
        var decision = AiDescriptionQualityService.Resolve(step, session, modelOutput, _settings);

        await Dispatcher.InvokeAsync(() =>
        {
            step.Description = decision.Text;
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
        SyncCurrentTitleFromEditor();
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

        SyncCurrentTitleFromEditor();
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
        var acceptedAi = 0;
        var groundedFallbacks = 0;
        try
        {
            foreach (var step in _current.Steps)
            {
                var modelOutput = await _describer.ImproveAsync(step, _current, _settings);
                var decision = AiDescriptionQualityService.Resolve(step, _current, modelOutput, _settings);
                step.Description = decision.Text;
                if (decision.UsedAi) acceptedAi++;
                else groundedFallbacks++;
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
            $"Description review complete using {_settings.AiProvider}.\n\n" +
            $"Accepted grounded AI descriptions: {acceptedAi}\n" +
            $"Protected by deterministic fallback: {groundedFallbacks}\n\n" +
            "SoplyraAI keeps the stronger local description whenever a model returns generic, uncertain, or ungrounded wording.",
            "SoplyraAI");
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_current.Steps.Count == 0)
        {
            MessageBox.Show("Capture at least one step before exporting.", "SoplyraAI");
            return;
        }

        SyncCurrentTitleFromEditor();
        var format = (ExportFormatBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PDF";
        await PerformExportAsync(_current, format);
    }

    private async void ExportSidebarGuide_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: GuideSession session }) return;

        var exportSession = session;
        if (session.Id == _current.Id)
        {
            SyncCurrentTitleFromEditor();
            exportSession = _current;
        }

        if (exportSession.Steps.Count == 0)
        {
            MessageBox.Show("This guide does not contain any recorded steps to export.", "SoplyraAI");
            return;
        }

        var format = (ExportFormatBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PDF";
        await PerformExportAsync(exportSession, format);
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
            _sessions.Save(session);
            var folder = ExportService.NewExportFolder(session);
            var outputs = new List<string>();
            var warnings = new List<string>();

            string Named(string generatedPath) => ExportFileNaming.RenameGeneratedFile(session, generatedPath);

            if (formatOption.Contains("PDF") && !formatOption.Contains("All"))
            {
                var pdf = await _pdfExporter.ExportAsync(session, folder);
                if (pdf is null)
                    throw new InvalidOperationException("PDF generation did not produce a file. Word and HTML export remain available.");
                outputs.Add(Named(pdf));
            }
            else if (formatOption.Contains("Word") || formatOption.Contains("docx"))
            {
                outputs.Add(Named(_exporter.ExportDocx(session, folder)));
            }
            else if (formatOption.Contains("HTML") || formatOption.Contains("Web"))
            {
                outputs.Add(Named(_exporter.ExportHtml(session, folder)));
            }
            else if (formatOption.Contains("Markdown") || formatOption.Contains("MD"))
            {
                outputs.Add(Named(_exporter.ExportMarkdown(session, folder)));
            }
            else
            {
                outputs.Add(Named(_exporter.ExportHtml(session, folder)));
                outputs.Add(Named(_exporter.ExportDocx(session, folder)));
                outputs.Add(Named(_exporter.ExportMarkdown(session, folder)));

                var pdf = await _pdfExporter.ExportAsync(session, folder);
                if (pdf is not null)
                    outputs.Add(Named(pdf));
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

    private void GuideTitle_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || sender is not TextBox box) return;
        var title = PrivacySanitizer.Clean(box.Text, 200).Trim();
        if (string.IsNullOrWhiteSpace(title) || string.Equals(title, _current.Title, StringComparison.Ordinal)) return;

        _current.Title = title;
        _sessions.Save(_current);
    }

    private void GuideTitle_LostFocus(object sender, RoutedEventArgs e)
    {
        SyncCurrentTitleFromEditor();
        RefreshSessions(selectCurrent: true);
    }

    private void SyncCurrentTitleFromEditor()
    {
        var title = PrivacySanitizer.Clean(GuideTitle.Text, 200).Trim();
        _current.Title = string.IsNullOrWhiteSpace(title) ? "Untitled guide" : title;
        if (!string.Equals(GuideTitle.Text, _current.Title, StringComparison.Ordinal))
            GuideTitle.Text = _current.Title;
        _sessions.Save(_current);
    }

    private void SessionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SessionList.SelectedItem is not GuideSession selected || selected.Id == _current.Id) return;
        if (_recorder.IsRecording) StopRecording();
        SyncCurrentTitleFromEditor();
        _sessions.Save(_current);
        _current = selected;
        BindCurrent();
    }
}
