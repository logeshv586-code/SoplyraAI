using System.Collections.ObjectModel;

namespace SoplyraAI.Models;

public sealed class GuideSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Untitled guide";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public string SessionFolder { get; set; } = "";
    public ObservableCollection<GuideStep> Steps { get; set; } = new();

    public override string ToString() => $"{Title}  ·  {CreatedAt:dd MMM HH:mm}";
}
