using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

internal static class StepCardLayoutBehavior
{
    [ModuleInitializer]
    internal static void Register()
    {
        EventManager.RegisterClassHandler(
            typeof(TextBox),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnTextBoxLoaded));

        EventManager.RegisterClassHandler(
            typeof(Image),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnImageLoaded));
    }

    private static void OnTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not GuideStep) return;

        var binding = BindingOperations.GetBinding(textBox, TextBox.TextProperty);
        var path = binding?.Path?.Path;

        if (string.Equals(path, nameof(GuideStep.Title), StringComparison.Ordinal))
        {
            textBox.TextWrapping = TextWrapping.Wrap;
            textBox.AcceptsReturn = false;
            textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            textBox.MaxHeight = double.PositiveInfinity;
        }
        else if (string.Equals(path, nameof(GuideStep.Description), StringComparison.Ordinal))
        {
            // Do not hide generated instructions behind a 120px editor. The step card should grow
            // naturally with the description so the complete wording remains visible and editable.
            textBox.TextWrapping = TextWrapping.Wrap;
            textBox.AcceptsReturn = true;
            textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            textBox.MaxHeight = double.PositiveInfinity;
            textBox.MinHeight = 74;
        }
    }

    private static void OnImageLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image image && image.DataContext is GuideStep)
        {
            // Uniform keeps the whole captured screen visible instead of cropping its edges.
            image.Stretch = Stretch.Uniform;
        }
    }
}
