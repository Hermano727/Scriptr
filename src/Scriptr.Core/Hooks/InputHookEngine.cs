using System.Diagnostics;
using System.Threading.Channels;
using Scriptr.Core.Events;
using Scriptr.Core.Native;

namespace Scriptr.Core.Hooks;

public sealed class InputHookEngine : IDisposable
{
    private readonly MouseHook    _mouseHook;
    private readonly KeyboardHook _keyboardHook;
    private readonly Channel<InputEvent> _channel;
    private readonly Func<ushort, bool>? _isControlKey;

    private Thread? _hookThread;
    private uint    _hookThreadId;
    private bool    _disposed;

    public ChannelReader<InputEvent> Events => _channel.Reader;

    public event Action<ushort>? ControlKeyPressed;

    public InputHookEngine(Func<ushort, bool>? isControlKey = null)
    {
        _isControlKey = isControlKey;
        _channel = Channel.CreateBounded<InputEvent>(new BoundedChannelOptions(4096)
        {
            FullMode    = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = true
        });
        _mouseHook    = new MouseHook();
        _keyboardHook = new KeyboardHook();
        _keyboardHook.ControlKeyPressed += vk => ControlKeyPressed?.Invoke(vk);
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        long startTick = Stopwatch.GetTimestamp();
        _hookThread = new Thread(() => HookThreadProc(startTick))
        {
            IsBackground = true,
            Name         = "ScriptrHookThread"
        };
        _hookThread.Start();
    }

    private void HookThreadProc(long startTick)
    {
        Volatile.Write(ref _hookThreadId, NativeMethods.GetCurrentThreadId());
        IntPtr hModule = NativeMethods.GetModuleHandle(null);

        try
        {
            _mouseHook.Install(hModule, _channel.Writer, startTick);
            _keyboardHook.Install(hModule, _channel.Writer, startTick, _isControlKey);

            // Win32 message pump required for WH_MOUSE_LL and WH_KEYBOARD_LL callbacks to fire.
            // GetMessage blocks until a message arrives; WM_QUIT breaks the loop.
            while (NativeMethods.GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }
        finally
        {
            _mouseHook.Uninstall();
            _keyboardHook.Uninstall();
            _channel.Writer.TryComplete();
        }
    }

    public void Stop()
    {
        // Spin briefly to ensure the hook thread has written its ID before we post to it
        SpinWait.SpinUntil(() => Volatile.Read(ref _hookThreadId) != 0, millisecondsTimeout: 2000);
        uint id = Volatile.Read(ref _hookThreadId);
        if (id != 0)
            NativeMethods.PostThreadMessage(id, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _hookThread?.Join(TimeSpan.FromSeconds(3));
    }
}
