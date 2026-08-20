using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SoplyraAI.Services;

public sealed class GlobalMouseHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private NativeMethods.LowLevelMouseProc? _proc;

    public event EventHandler<MouseActionEventArgs>? MouseAction;

    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;
        _proc = HookCallback;
        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule;
        var moduleHandle = NativeMethods.GetModuleHandle(currentModule?.ModuleName);
        _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, moduleHandle, 0);
        if (_hookId == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not install global mouse hook.");
    }

    public void Stop()
    {
        if (_hookId == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = wParam.ToInt32();
            string? action = msg switch
            {
                NativeMethods.WM_LBUTTONDOWN => "Click",
                NativeMethods.WM_RBUTTONDOWN => "Right-click",
                NativeMethods.WM_MBUTTONDOWN => "Middle-click",
                _ => null
            };

            if (action is not null)
            {
                var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                MouseAction?.Invoke(this, new MouseActionEventArgs(action, data.pt.x, data.pt.y));
            }
        }
        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose() => Stop();
}

public sealed record MouseActionEventArgs(string Action, int X, int Y);
