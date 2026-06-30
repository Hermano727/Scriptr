using System.Runtime.InteropServices;

namespace Scriptr.Core.Native;

internal delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

internal static class NativeMethods
{
    internal const int WH_MOUSE_LL    = 14;
    internal const int WH_KEYBOARD_LL = 13;
    internal const int HC_ACTION      = 0;

    internal const int SM_XVIRTUALSCREEN  = 76;
    internal const int SM_YVIRTUALSCREEN  = 77;
    internal const int SM_CXVIRTUALSCREEN = 78;
    internal const int SM_CYVIRTUALSCREEN = 79;

    internal const uint WM_QUIT       = 0x0012;
    internal const uint WM_MOUSEMOVE  = 0x0200;
    internal const uint WM_LBUTTONDOWN = 0x0201;
    internal const uint WM_LBUTTONUP   = 0x0202;
    internal const uint WM_RBUTTONDOWN = 0x0204;
    internal const uint WM_RBUTTONUP   = 0x0205;
    internal const uint WM_MBUTTONDOWN = 0x0207;
    internal const uint WM_MBUTTONUP   = 0x0208;
    internal const uint WM_MOUSEWHEEL  = 0x020A;
    internal const uint WM_XBUTTONDOWN = 0x020B;
    internal const uint WM_XBUTTONUP   = 0x020C;
    internal const uint WM_KEYDOWN     = 0x0100;
    internal const uint WM_KEYUP       = 0x0101;
    internal const uint WM_SYSKEYDOWN  = 0x0104;
    internal const uint WM_SYSKEYUP    = 0x0105;

    // MSLLHOOKSTRUCT.flags bits
    internal const uint LLMHF_INJECTED          = 0x00000001;
    internal const uint LLMHF_LOWER_IL_INJECTED = 0x00000002;

    // KBDLLHOOKSTRUCT.flags bits
    internal const uint LLKHF_EXTENDED          = 0x01;
    internal const uint LLKHF_LOWER_IL_INJECTED = 0x02;
    internal const uint LLKHF_INJECTED          = 0x10;
    internal const uint LLKHF_ALTDOWN           = 0x20;
    internal const uint LLKHF_UP                = 0x80;

    // XBUTTON identifiers — used both in WM_XBUTTON* mouseData (recording)
    // and in MOUSEINPUT.mouseData for MOUSEEVENTF_XDOWN/XUP (playback).
    internal const ushort XBUTTON1 = 0x0001;
    internal const ushort XBUTTON2 = 0x0002;

    // DPI awareness context handle value for Per-Monitor v2
    internal static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);

    // ── SendInput ────────────────────────────────────────────────────────────

    internal const uint INPUT_MOUSE    = 0;
    internal const uint INPUT_KEYBOARD = 1;

    // MOUSEEVENTF flags
    internal const uint MOUSEEVENTF_MOVE        = 0x0001;
    internal const uint MOUSEEVENTF_LEFTDOWN    = 0x0002;
    internal const uint MOUSEEVENTF_LEFTUP      = 0x0004;
    internal const uint MOUSEEVENTF_RIGHTDOWN   = 0x0008;
    internal const uint MOUSEEVENTF_RIGHTUP     = 0x0010;
    internal const uint MOUSEEVENTF_MIDDLEDOWN  = 0x0020;
    internal const uint MOUSEEVENTF_MIDDLEUP    = 0x0040;
    internal const uint MOUSEEVENTF_XDOWN       = 0x0080;
    internal const uint MOUSEEVENTF_XUP         = 0x0100;
    internal const uint MOUSEEVENTF_WHEEL       = 0x0800;
    internal const uint MOUSEEVENTF_HWHEEL      = 0x1000;
    internal const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
    internal const uint MOUSEEVENTF_ABSOLUTE    = 0x8000;

    // KEYEVENTF flags
    internal const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    internal const uint KEYEVENTF_KEYUP       = 0x0002;
    internal const uint KEYEVENTF_SCANCODE    = 0x0008;

    // Panic key virtual code
    internal const ushort VK_PAUSE = 0x13;

    // Hotkey modifiers (RegisterHotKey fsModifiers bitmask)
    internal const uint MOD_NONE     = 0x0000;
    internal const uint MOD_ALT      = 0x0001;
    internal const uint MOD_CONTROL  = 0x0002;
    internal const uint MOD_SHIFT    = 0x0004;
    internal const uint MOD_WIN      = 0x0008;
    internal const uint MOD_NOREPEAT = 0x4000;   // suppress auto-repeat WM_HOTKEY floods

    internal const int WM_HOTKEY = 0x0312;

    // Cached struct size — computed once, used in every SendInput call
    internal static readonly int InputSize = Marshal.SizeOf<INPUT>();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    internal static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    internal static extern IntPtr DispatchMessage(ref MSG lpmsg);

    [DllImport("user32.dll")]
    internal static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    internal static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    // Returns a short: high bit (0x8000) set = key is currently held down.
    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
