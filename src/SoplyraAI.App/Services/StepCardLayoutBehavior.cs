using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
            typeof(TextBox),
            TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler(OnTextBoxTextChanged));

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
            // Do not hide generated instructions behind a fixed editor. The step card grows with
            // the description so all wording remains visible and can be edited in-place.
            textBox.TextWrapping = TextWrapping.Wrap;
            textBox.AcceptsReturn = true;
            textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            textBox.MaxHeight = double.PositiveInfinity;
            textBox.MinHeight = 74;
        }
    }

    private static void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        // Binding refreshes also raise TextChanged. Only treat keyboard-focused changes as a real
        // user edit; this prevents background AI/property refreshes from marking generated text as
        // manual while making double-click/click-then-type edits authoritative immediately.
        if (sender is not TextBox textBox ||
            textBox.DataContext is not GuideStep step ||
            !textBox.IsKeyboardFocusWithin)
            return;

        var binding = BindingOperations.GetBinding(textBox, TextBox.TextProperty);
        var path = binding?.Path?.Path;

        if (string.Equals(path, nameof(GuideStep.Title), StringComparison.Ordinal))
            step.ApplyUserTitle(textBox.Text);
        else if (string.Equals(path, nameof(GuideStep.Description), StringComparison.Ordinal))
            step.ApplyUserDescription(textBox.Text);
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
