using Scriptr.Core.Native;

namespace Scriptr.Core;

/// <summary>
/// Public surface for host-level platform initialisation that entry-point projects
/// (Scriptr.Cli, Scriptr.Stub) need before any screen-metric or hook calls.
/// Keeps NativeMethods internal while still allowing callers to bootstrap correctly.
/// </summary>
public static class Platform
{
    /// <summary>
    /// Sets Per-Monitor v2 DPI awareness for the current process.
    /// Must be called before any <see cref="GetVirtualScreen"/> or hook installation.
    /// </summary>
    public static void InitDpiAwareness() =>
        NativeMethods.SetProcessDpiAwarenessContext(
            NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    /// <summary>
    /// Returns the bounds of the Win32 virtual screen that spans all monitors.
    /// Coordinates are in physical pixels (requires <see cref="InitDpiAwareness"/> first).
    /// </summary>
    public static (int Left, int Top, int Width, int Height) GetVirtualScreen() => (
        NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN)
    );

    // Hotkey bridge — public surface so Scriptr.Gui (separate assembly) can call
    // RegisterHotKey/UnregisterHotKey without access to internal NativeMethods.
    public const int  WM_HOTKEY    = 0x0312;
    public const uint MOD_NONE     = 0x0000;
    public const uint MOD_ALT      = 0x0001;
    public const uint MOD_CONTROL  = 0x0002;
    public const uint MOD_SHIFT    = 0x0004;
    public const uint MOD_WIN      = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    public static bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, ushort vk) =>
        NativeMethods.RegisterHotKey(hwnd, id, modifiers, vk);

    public static bool UnregisterHotKey(IntPtr hwnd, int id) =>
        NativeMethods.UnregisterHotKey(hwnd, id);
}
