namespace SoplyraAI.Services;

public static class PathSecurity
{
    private const long MaxScreenshotBytes = 25L * 1024 * 1024;
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static bool IsWithinRoot(string root, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(candidate))
            return false;

        try
        {
            var rootFull = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var candidateFull = Path.GetFullPath(candidate);

            return candidateFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool HasReparsePoint(string root, string candidate)
    {
        try
        {
            var rootFull = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var candidateFull = Path.GetFullPath(candidate);

            if (!IsWithinRoot(rootFull, candidateFull) &&
                !candidateFull.Equals(rootFull, StringComparison.OrdinalIgnoreCase))
                return true;

            if (ExistsAndIsReparsePoint(rootFull)) return true;

            var relative = Path.GetRelativePath(rootFull, candidateFull);
            if (relative == ".") return false;

            var current = rootFull;
            foreach (var part in relative.Split(
                         new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                if (ExistsAndIsReparsePoint(current)) return true;
            }

            return false;
        }
        catch
        {
            return true;
        }
    }

    public static bool TryGetTrustedPng(
        string sessionFolder,
        string? candidate,
        out string trustedPath,
        bool requireExists = true)
    {
        trustedPath = "";
        if (string.IsNullOrWhiteSpace(candidate)) return false;

        try
        {
            var imagesRoot = Path.Combine(Path.GetFullPath(sessionFolder), "images");
            var full = Path.GetFullPath(candidate);

            if (!IsWithinRoot(imagesRoot, full)) return false;
            if (!Path.GetExtension(full).Equals(".png", StringComparison.OrdinalIgnoreCase)) return false;
            if (HasReparsePoint(imagesRoot, full)) return false;

            if (requireExists)
            {
                var info = new FileInfo(full);
                if (!info.Exists || info.Length < PngSignature.Length || info.Length > MaxScreenshotBytes)
                    return false;

                Span<byte> signature = stackalloc byte[PngSignature.Length];
                using var stream = new FileStream(
                    full, FileMode.Open, FileAccess.Read, FileShare.Read,
                    4096, FileOptions.SequentialScan);
                if (stream.Read(signature) != signature.Length ||
                    !signature.SequenceEqual(PngSignature))
                    return false;
            }

            trustedPath = full;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ExistsAndIsReparsePoint(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return false;
        return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }
}
