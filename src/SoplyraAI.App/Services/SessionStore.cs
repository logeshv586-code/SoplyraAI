using System.Text.Json;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

public sealed class SessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string RootFolder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoplyraAI", "Sessions");

    public GuideSession Create(string title)
    {
        var session = new GuideSession { Title = title };
        session.SessionFolder = Path.Combine(RootFolder, session.Id.ToString("N"));
        Directory.CreateDirectory(Path.Combine(session.SessionFolder, "images"));
        Save(session);
        return session;
    }

    public void Save(GuideSession session)
    {
        session.UpdatedAt = DateTimeOffset.Now;
        Directory.CreateDirectory(session.SessionFolder);
        var json = JsonSerializer.Serialize(session, JsonOptions);
        File.WriteAllText(Path.Combine(session.SessionFolder, "session.json"), json);
    }

    public IReadOnlyList<GuideSession> LoadAll()
    {
        if (!Directory.Exists(RootFolder)) return Array.Empty<GuideSession>();
        var result = new List<GuideSession>();
        foreach (var file in Directory.EnumerateFiles(RootFolder, "session.json", SearchOption.AllDirectories))
        {
            try
            {
                var session = JsonSerializer.Deserialize<GuideSession>(File.ReadAllText(file), JsonOptions);
                if (session is not null)
                {
                    session.SessionFolder = Path.GetDirectoryName(file)!;
                    result.Add(session);
                }
            }
            catch { }
        }
        return result.OrderByDescending(x => x.UpdatedAt).ToList();
    }
}
