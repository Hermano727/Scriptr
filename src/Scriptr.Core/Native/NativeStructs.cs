using System.Runtime.InteropServices;

namespace Scriptr.Core.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MSLLHOOKSTRUCT
{
    public POINT  pt;
    public uint   mouseData;
    public uint   flags;
    public uint   time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KBDLLHOOKSTRUCT
{
    public uint   vkCode;
    public uint   scanCode;
    public uint   flags;
    public uint   time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    public IntPtr hwnd;
    public uint   message;
    public IntPtr wParam;
    public IntPtr lParam;
    public uint   time;
    public POINT  pt;
}

// ── SendInput structs ────────────────────────────────────────────────────────

// On x64: 4+4+4+4+4+(4 pad)+8 = 32 bytes. IntPtr carries the 8-byte alignment.
[StructLayout(LayoutKind.Sequential)]
internal struct MOUSEINPUT
{
    public int    dx;
    public int    dy;
    public uint   mouseData;
    public uint   dwFlags;
    public uint   time;
    public IntPtr dwExtraInfo;
}

// On x64: 2+2+4+4+(4 pad)+8 = 24 bytes.
[StructLayout(LayoutKind.Sequential)]
internal struct KEYBDINPUT
{
    public ushort wVk;
    public ushort wScan;
    public uint   dwFlags;
    public uint   time;
    public IntPtr dwExtraInfo;
}

// Union: both members start at offset 0. Size = max(MOUSEINPUT, KEYBDINPUT) = 32 bytes.
[StructLayout(LayoutKind.Explicit)]
internal struct INPUTUNION
{
    [FieldOffset(0)] public MOUSEINPUT mi;
    [FieldOffset(0)] public KEYBDINPUT ki;
}

// Sequential lets the runtime insert the 4-byte pad between Type and Data so that
// Data aligns to 8 bytes (required by IntPtr members inside the union).
// Total on x64: 4(Type) + 4(pad) + 32(INPUTUNION) = 40 bytes.
[StructLayout(LayoutKind.Sequential)]
internal struct INPUT
{
    public uint       Type;
    public INPUTUNION Data;
}
