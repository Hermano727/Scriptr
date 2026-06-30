using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Scriptr.Core.Events;
using Scriptr.Core.Native;

namespace Scriptr.Core.Hooks;

internal sealed class MouseHook
{
    // CRITICAL: Stored as an instance field so the GC cannot collect this delegate
    // while the native hook (which holds only an unmanaged function pointer) is active.
    private readonly HookProc _callback;

    private IntPtr _handle = IntPtr.Zero;
    private ChannelWriter<InputEvent>? _writer;
    private long _startTick;

    // Virtual screen bounds cached at install time — queried once per session
    private int _vsLeft;
    private int _vsTop;
    private int _vsWidth;
    private int _vsHeight;

    public MouseHook()
    {
        _callback = Callback;
    }

    public void Install(IntPtr hModule, ChannelWriter<InputEvent> writer, long startTick)
    {
        _writer    = writer;
        _startTick = startTick;
        _vsLeft    = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        _vsTop     = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        _vsWidth   = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        _vsHeight  = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

        _handle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _callback, hModule, 0);
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException(
                $"SetWindowsHookEx(WH_MOUSE_LL) failed. Win32 error: {Marshal.GetLastWin32Error()}");
    }

    public void Uninstall()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_handle);
            _handle = IntPtr.Zero;
        }
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode != NativeMethods.HC_ACTION)
            return NativeMethods.CallNextHookEx(_handle, nCode, wParam, lParam);

        var s = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

        // Skip events injected by playback engine or other tools
        if ((s.flags & (NativeMethods.LLMHF_INJECTED | NativeMethods.LLMHF_LOWER_IL_INJECTED)) != 0)
            return NativeMethods.CallNextHookEx(_handle, nCode, wParam, lParam);

        long tsUs  = ComputeTimestampUs();
        float normX = Math.Clamp((s.pt.X - _vsLeft) / (float)_vsWidth,  0f, 1f);
        float normY = Math.Clamp((s.pt.Y - _vsTop)  / (float)_vsHeight, 0f, 1f);
        uint  msg   = (uint)wParam.ToInt64();

        InputEvent evt = msg switch
        {
            NativeMethods.WM_MOUSEMOVE =>
                new InputEvent(InputEventType.MouseMove, tsUs, normX, normY, MouseButton.None, 0, 0, 0, KeyFlags.None),

            NativeMethods.WM_LBUTTONDOWN =>
                new InputEvent(InputEventType.MouseDown, tsUs, normX, normY, MouseButton.Left, 0, 0, 0, KeyFlags.None),
            NativeMethods.WM_LBUTTONUP =>
                new InputEvent(InputEventType.MouseUp, tsUs, normX, normY, MouseButton.Left, 0, 0, 0, KeyFlags.None),

            NativeMethods.WM_RBUTTONDOWN =>
                new InputEvent(InputEventType.MouseDown, tsUs, normX, normY, MouseButton.Right, 0, 0, 0, KeyFlags.None),
            NativeMethods.WM_RBUTTONUP =>
                new InputEvent(InputEventType.MouseUp, tsUs, normX, normY, MouseButton.Right, 0, 0, 0, KeyFlags.None),

            NativeMethods.WM_MBUTTONDOWN =>
                new InputEvent(InputEventType.MouseDown, tsUs, normX, normY, MouseButton.Middle, 0, 0, 0, KeyFlags.None),
            NativeMethods.WM_MBUTTONUP =>
                new InputEvent(InputEventType.MouseUp, tsUs, normX, normY, MouseButton.Middle, 0, 0, 0, KeyFlags.None),

            NativeMethods.WM_MOUSEWHEEL =>
                new InputEvent(InputEventType.MouseWheel, tsUs, normX, normY, MouseButton.None,
                    (short)((s.mouseData >> 16) & 0xFFFF), 0, 0, KeyFlags.None),

            NativeMethods.WM_XBUTTONDOWN =>
                new InputEvent(InputEventType.MouseDown, tsUs, normX, normY, XButtonFromMouseData(s.mouseData), 0, 0, 0, KeyFlags.None),
            NativeMethods.WM_XBUTTONUP =>
                new InputEvent(InputEventType.MouseUp, tsUs, normX, normY, XButtonFromMouseData(s.mouseData), 0, 0, 0, KeyFlags.None),

            _ => default
        };

        if (evt.EventType != default || msg == NativeMethods.WM_MOUSEMOVE)
            _writer!.TryWrite(evt);

        return NativeMethods.CallNextHookEx(_handle, nCode, wParam, lParam);
    }

    private static MouseButton XButtonFromMouseData(uint mouseData)
    {
        ushort xBtn = (ushort)(mouseData >> 16);
        return xBtn == NativeMethods.XBUTTON1 ? MouseButton.X1 : MouseButton.X2;
    }

    private long ComputeTimestampUs()
    {
        long elapsed = Stopwatch.GetTimestamp() - _startTick;
        return (long)(elapsed * 1_000_000.0 / Stopwatch.Frequency);
    }
}
