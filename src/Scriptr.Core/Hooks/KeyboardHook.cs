using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Scriptr.Core.Events;
using Scriptr.Core.Native;

namespace Scriptr.Core.Hooks;

internal sealed class KeyboardHook
{
    // CRITICAL: Stored as an instance field so the GC cannot collect this delegate
    // while the native hook (which holds only an unmanaged function pointer) is active.
    private readonly HookProc _callback;

    private IntPtr _handle = IntPtr.Zero;
    private ChannelWriter<InputEvent>? _writer;
    private long _startTick;
    private Func<ushort, bool>? _isControlKey;

    public event Action<ushort>? ControlKeyPressed;

    public KeyboardHook()
    {
        _callback = Callback;
    }

    public void Install(
        IntPtr hModule,
        ChannelWriter<InputEvent> writer,
        long startTick,
        Func<ushort, bool>? isControlKey = null)
    {
        _writer       = writer;
        _startTick    = startTick;
        _isControlKey = isControlKey;

        _handle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _callback, hModule, 0);
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException(
                $"SetWindowsHookEx(WH_KEYBOARD_LL) failed. Win32 error: {Marshal.GetLastWin32Error()}");
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

        var s = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

        // Skip events injected by playback engine or other tools
        if ((s.flags & (NativeMethods.LLKHF_INJECTED | NativeMethods.LLKHF_LOWER_IL_INJECTED)) != 0)
            return NativeMethods.CallNextHookEx(_handle, nCode, wParam, lParam);

        ushort vk = (ushort)s.vkCode;

        // Hotkey omission: consume the key system-wide and fire the control event
        if (_isControlKey?.Invoke(vk) == true)
        {
            ControlKeyPressed?.Invoke(vk);
            return new IntPtr(1);
        }

        uint msg = (uint)wParam.ToInt64();
        InputEventType eventType = msg switch
        {
            NativeMethods.WM_KEYDOWN    => InputEventType.KeyDown,
            NativeMethods.WM_KEYUP      => InputEventType.KeyUp,
            NativeMethods.WM_SYSKEYDOWN => InputEventType.SysKeyDown,
            NativeMethods.WM_SYSKEYUP   => InputEventType.SysKeyUp,
            _                           => InputEventType.KeyDown
        };

        KeyFlags flags = KeyFlags.None;
        if ((s.flags & NativeMethods.LLKHF_EXTENDED) != 0) flags |= KeyFlags.Extended;
        if ((s.flags & NativeMethods.LLKHF_ALTDOWN)  != 0) flags |= KeyFlags.AltDown;

        long tsUs = (long)((Stopwatch.GetTimestamp() - _startTick) * 1_000_000.0 / Stopwatch.Frequency);

        var evt = new InputEvent(
            eventType,
            tsUs,
            0f, 0f,
            MouseButton.None,
            0,
            vk,
            (ushort)s.scanCode,
            flags);

        _writer!.TryWrite(evt);
        return NativeMethods.CallNextHookEx(_handle, nCode, wParam, lParam);
    }
}
