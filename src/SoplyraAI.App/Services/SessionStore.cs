using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

public sealed class SessionStore
{
    private const long MaxSessionJsonBytes = 5L * 1024 * 1024;
    private const int MaxSteps = 5000;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, MaxDepth = 32 };

    public string RootFolder { get; }

    public SessionStore(string? rootFolder = null)
    {
        RootFolder = rootFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoplyraAI", "Sessions");
    }

    public GuideSession Create(string title)
    {
        var session = new GuideSession { Title = PrivacySanitizer.Clean(title, 200) };
        session.SessionFolder = GetSessionFolder(session.Id);
        Directory.CreateDirectory(Path.Combine(session.SessionFolder, "images"));
        Save(session);
        return session;
    }

    public void Save(GuideSession session)
    {
        if (session.Steps.Count > MaxSteps) throw new InvalidOperationException($"A guide cannot contain more than {MaxSteps} steps.");
        var folder = GetSessionFolder(session.Id);
        if (Directory.Exists(folder) && PathSecurity.HasReparsePoint(RootFolder, folder)) throw new InvalidOperationException("The session directory is not a trusted local folder.");
        var imagesFolder = Path.Combine(folder, "images");
        Directory.CreateDirectory(imagesFolder);
        if (PathSecurity.HasReparsePoint(folder, imagesFolder)) throw new InvalidOperationException("The session image directory is not a trusted local folder.");

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

                // A folder without session.json is a previously deleted workflow whose screenshot
                // files may have been temporarily locked by Windows/WPF. It must never reappear in
                // Saved Workflows; clean it up opportunistically when the lock is gone.
                if (!info.Exists)
                {
                    TryCleanupDeletedFolder(folder);
                    continue;
                }

                if (info.Length < 2 || info.Length > MaxSessionJsonBytes) continue;
                if (PathSecurity.HasReparsePoint(RootFolder, file)) continue;
                var session = JsonSerializer.Deserialize<GuideSession>(File.ReadAllText(file, Encoding.UTF8), JsonOptions);
                if (session is null || session.Id != expectedId || session.Steps.Count > MaxSteps) continue;
                session.SessionFolder = Path.GetFullPath(folder);
                SanitizeSession(session, requireExistingScreenshots: true);
                result.Add(session);
            }
            catch { }
        }
        return result.OrderByDescending(x => x.UpdatedAt).ToList();
    }

    public bool DeleteStep(GuideSession session, Guid stepId)
    {
        ArgumentNullException.ThrowIfNull(session);

        var index = -1;
        for (var i = 0; i < session.Steps.Count; i++)
        {
            if (session.Steps[i].Id == stepId)
            {
                index = i;
                break;
            }
        }
        if (index < 0) return false;

        var step = session.Steps[index];
        string? trustedScreenshot = null;
        if (PathSecurity.TryGetTrustedPng(session.SessionFolder, step.ScreenshotPath, out var trusted, requireExists: false))
            trustedScreenshot = trusted;

        session.Steps.RemoveAt(index);
        Renumber(session.Steps);

        try
        {
            Save(session);
        }
        catch
        {
            session.Steps.Insert(Math.Min(index, session.Steps.Count), step);
            Renumber(session.Steps);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(trustedScreenshot))
        {
            try
            {
                if (File.Exists(trustedScreenshot) && !PathSecurity.HasReparsePoint(session.SessionFolder, trustedScreenshot))
                    File.Delete(trustedScreenshot);
            }
            catch
            {
                // The step is already safely removed from session.json. An orphaned screenshot is preferable
                // to making the user's Remove action fail because Windows temporarily locked the image file.
            }
        }

        return true;
    }

    public bool Delete(Guid id)
    {
        var folder = GetSessionFolder(id);
        try
        {
            if (!Directory.Exists(folder)) return true;
            if (PathSecurity.HasReparsePoint(RootFolder, folder)) return false;
            var images = Path.Combine(folder, "images");
            if (Directory.Exists(images) && PathSecurity.HasReparsePoint(folder, images)) return false;

            // Remove session.json first. This is the authoritative logical delete: once this file
            // is gone the workflow cannot be loaded or reappear in Saved Workflows, even if one
            // screenshot is still held open by the WPF image decoder or antivirus software.
            var sessionFile = Path.Combine(folder, "session.json");
            if (File.Exists(sessionFile))
            {
                if (PathSecurity.HasReparsePoint(folder, sessionFile)) return false;
                File.Delete(sessionFile);
            }

            TryCleanupDeletedFolder(folder);
            return !File.Exists(sessionFile);
        }
        catch
        {
            return false;
        }
    }

    private string GetSessionFolder(Guid id) => Path.Combine(RootFolder, id.ToString("N"));

    private static void TryCleanupDeletedFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch
        {
            // Logical deletion is already complete because session.json is absent. The remaining
            // orphaned image directory is retried on the next LoadAll/application start.
        }
    }

    private static void SanitizeSession(GuideSession session, bool requireExistingScreenshots)
    {
        session.Title = PrivacySanitizer.Clean(session.Title, 200);
        if (string.IsNullOrWhiteSpace(session.Title)) session.Title = "Untitled guide";
        session.DocumentationMode = session.DocumentationMode == "Detailed" ? "Detailed" : "Quick";
        session.Steps ??= new ObservableCollection<GuideStep>();

        foreach (var step in session.Steps)
        {
            step.Action = PrivacySanitizer.Clean(step.Action, 40);
            step.Title = PrivacySanitizer.Clean(step.Title, 240);
            step.Description = PrivacySanitizer.Clean(step.Description, 4000);
            step.Context ??= new UiContext();
            PrivacySanitizer.SanitizeContext(step.Context);
            if (!PathSecurity.TryGetTrustedPng(session.SessionFolder, step.ScreenshotPath, out var trustedScreenshot, requireExistingScreenshots)) step.ScreenshotPath = "";
            else step.ScreenshotPath = trustedScreenshot;
        }

        Renumber(session.Steps);
    }

    private static void Renumber(IList<GuideStep> steps)
    {
        for (var i = 0; i < steps.Count; i++) steps[i].Number = i + 1;
    }
}
