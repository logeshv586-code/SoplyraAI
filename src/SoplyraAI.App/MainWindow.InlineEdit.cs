using SoplyraAI.Models;

namespace SoplyraAI;

public partial class MainWindow
{
    internal void CommitInlineEdit(GuideStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (!_current.Steps.Any(existing => existing.Id == step.Id)) return;

        step.DocumentationStatus = "✓ Manual edit kept";
        _sessions.Save(_current);
    }
}
