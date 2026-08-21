using System.Text;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

internal static class ExportFileNaming
{
    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string GetBaseName(GuideSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var title = PrivacySanitizer.Clean(session.Title, 120).Trim();
        if (string.IsNullOrWhiteSpace(title)) title = "SoplyraAI Guide";

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(title.Length);
        var previousSpace = false;
        foreach (var ch in title)
        {
            var replacement = invalid.Contains(ch) || char.IsControl(ch) ? '_' : ch;
            if (char.IsWhiteSpace(replacement))
            {
                if (previousSpace) continue;
                replacement = ' ';
                previousSpace = true;
            }
            else
            {
                previousSpace = false;
            }
            builder.Append(replacement);
        }

        var safe = builder.ToString().Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(safe)) safe = "SoplyraAI Guide";
        if (ReservedWindowsNames.Contains(safe)) safe += " Guide";
        if (safe.Length > 100) safe = safe[..100].TrimEnd();
        return safe;
    }

    public static string RenameGeneratedFile(GuideSession session, string generatedPath)
    {
        if (string.IsNullOrWhiteSpace(generatedPath))
            throw new ArgumentException("Generated export path is empty.", nameof(generatedPath));
        if (!File.Exists(generatedPath))
            throw new FileNotFoundException("The generated export file could not be found.", generatedPath);

        var extension = Path.GetExtension(generatedPath);
        if (string.IsNullOrWhiteSpace(extension)) return generatedPath;

        var directory = Path.GetDirectoryName(generatedPath)
            ?? throw new InvalidOperationException("The generated export file does not have a parent folder.");
        var target = Path.Combine(directory, GetBaseName(session) + extension.ToLowerInvariant());

        if (string.Equals(
                Path.GetFullPath(generatedPath),
                Path.GetFullPath(target),
                StringComparison.OrdinalIgnoreCase))
            return generatedPath;

        File.Move(generatedPath, target, overwrite: true);
        return target;
    }
}
