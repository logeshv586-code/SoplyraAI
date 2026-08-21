using System.Windows;

namespace SoplyraAI.Views;

public partial class CaptureModeWindow : Window
{
    public string SelectedMode { get; private set; }

    public CaptureModeWindow(string currentMode)
    {
        InitializeComponent();
        DetailedRadio.IsChecked = currentMode.Equals("Detailed", StringComparison.OrdinalIgnoreCase);
        QuickRadio.IsChecked = DetailedRadio.IsChecked != true;
        SelectedMode = DetailedRadio.IsChecked == true ? "Detailed" : "Quick";
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        SelectedMode = DetailedRadio.IsChecked == true ? "Detailed" : "Quick";
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
