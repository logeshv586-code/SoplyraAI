namespace SoplyraAI.Services;

internal static class CaptureOverlayRegistry
{
    private static long _windowHandle;
    private static int _excludedByWindows;

    internal static void Register(IntPtr windowHandle, bool excludedByWindows)
    {
        Interlocked.Exchange(ref _windowHandle, windowHandle.ToInt64());
        Volatile.Write(ref _excludedByWindows, excludedByWindows ? 1 : 0);
    }

    internal static void Clear(IntPtr windowHandle)
    {
        var expected = windowHandle.ToInt64();
        if (Interlocked.Read(ref _windowHandle) == expected)
        {
            Interlocked.Exchange(ref _windowHandle, 0);
            Volatile.Write(ref _excludedByWindows, 0);
        }
    }

    internal static bool TryGet(out IntPtr windowHandle, out bool excludedByWindows)
    {
        windowHandle = new IntPtr(Interlocked.Read(ref _windowHandle));
        excludedByWindows = Volatile.Read(ref _excludedByWindows) == 1;
        return windowHandle != IntPtr.Zero;
    }
}
