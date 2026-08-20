using System.Windows;

namespace SoplyraAI.Views;

public partial class CaptureBarWindow : Window
{
    public event EventHandler? StopRequested;
    public event EventHandler? PauseRequested;
    private bool _paused;

    public CaptureBarWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Left = SystemParameters.WorkArea.Left + 18;
            Top = SystemParameters.WorkArea.Top + 18;
        };
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove(); };
    }

    public void SetStepCount(int count) => StepText.Text = $"{count} step{(count == 1 ? "" : "s")} captured";

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        _paused = !_paused;
        PauseButton.Content = _paused ? "Resume" : "Pause";
        PauseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void StopButton_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke(this, EventArgs.Empty);
}
