using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

public sealed class SessionStore
{
    private const long MaxSessionJsonBytes = 5L * 1024 * 1024;
    private const int MaxSteps = 5000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 32
    };

    public string RootFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoplyraAI", "Sessions");

    public GuideSession Create(string title)
    {
        var session = new GuideSession
        {
            Title = PrivacySanitizer.Clean(title, 200)
        };
        session.SessionFolder = GetSessionFolder(session.Id);
        Directory.CreateDirectory(Path.Combine(session.SessionFolder, "images"));
        Save(session);
        return session;
    }

    public void Save(GuideSession session)
    {
        if (session.Steps.Count > MaxSteps)
            throw new InvalidOperationException($"A guide cannot contain more than {MaxSteps} steps.");

        var folder = GetSessionFolder(session.Id);
        if (Directory.Exists(folder) && PathSecurity.HasReparsePoint(RootFolder, folder))
            throw new InvalidOperationException("The session directory is not a trusted local folder.");

        var imagesFolder = Path.Combine(folder, "images");
        Directory.CreateDirectory(imagesFolder);
        if (PathSecurity.HasReparsePoint(folder, imagesFolder))
            throw new InvalidOperationException("The session image directory is not a trusted local folder.");

        session.SessionFolder = folder;
        SanitizeSession(session, requireExistingScreenshots: false);
        session.UpdatedAt = DateTimeOffset.Now;

        var json = JsonSerializer.Serialize(session, JsonOptions);
        var path = Path.Combine(folder, "session.json");
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        File.WriteAllText(temp, json, new UTF8Encoding(false));
        File.Move(temp, path, true);
    }

    public IReadOnlyList<GuideSession> LoadAll()
    {
        if (!Directory.Exists(RootFolder)) return Array.Empty<GuideSession>();

        var result = new List<GuideSession>();
        foreach (var folder in Directory.EnumerateDirectories(RootFolder, "*", SearchOption.TopDirectoryOnly))
        {
            try
            {
                if (PathSecurity.HasReparsePoint(RootFolder, folder)) continue;

                var directoryName = Path.GetFileName(folder);
                if (!Guid.TryParseExact(directoryName, "N", out var expectedId)) continue;

                var file = Path.Combine(folder, "session.json");
                var info = new FileInfo(file);
                if (!info.Exists || info.Length < 2 || info.Length > MaxSessionJsonBytes) continue;
                if (PathSecurity.HasReparsePoint(RootFolder, file)) continue;

                var session = JsonSerializer.Deserialize<GuideSession>(
                    File.ReadAllText(file, Encoding.UTF8), JsonOptions);
                if (session is null || session.Id != expectedId || session.Steps.Count > MaxSteps) continue;

                session.SessionFolder = Path.GetFullPath(folder);
                SanitizeSession(session, requireExistingScreenshots: true);
                result.Add(session);
            }
            catch
            {
            }
        }

        return result.OrderByDescending(x => x.UpdatedAt).ToList();
    }

    private string GetSessionFolder(Guid id) =>
        Path.Combine(RootFolder, id.ToString("N"));

    private static void SanitizeSession(GuideSession session, bool requireExistingScreenshots)
    {
        session.Title = PrivacySanitizer.Clean(session.Title, 200);
        if (string.IsNullOrWhiteSpace(session.Title)) session.Title = "Untitled guide";

        var cleanSteps = new ObservableCollection<GuideStep>();
        foreach (var step in session.Steps.Take(MaxSteps))
        {
            step.Action = PrivacySanitizer.Clean(step.Action, 40);
            step.Title = PrivacySanitizer.Clean(step.Title, 240);
            step.Description = PrivacySanitizer.Clean(step.Description, 4000);
            step.Context ??= new UiContext();
            PrivacySanitizer.SanitizeContext(step.Context);

            if (!PathSecurity.TryGetTrustedPng(
                    session.SessionFolder,
                    step.ScreenshotPath,
                    out var trustedScreenshot,
                    requireExistingScreenshots))
            {
                step.ScreenshotPath = "";
            }
            else
            {
                step.ScreenshotPath = trustedScreenshot;
            }

            cleanSteps.Add(step);
        }

        session.Steps = cleanSteps;
        for (var i = 0; i < session.Steps.Count; i++)
            session.Steps[i].Number = i + 1;
    }
}
