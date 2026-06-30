using System.Diagnostics;
using System.Runtime.InteropServices;
using Scriptr.Core.Events;
using Scriptr.Core.Native;

namespace Scriptr.Core.Playback;

public sealed class PlaybackEngine : IDisposable
{
    // Pre-allocated single-element buffer — avoids one heap allocation per injected event.
    private readonly INPUT[] _inputBuffer = new INPUT[1];

    private IReadOnlyList<InputEvent> _events = Array.Empty<InputEvent>();
    private PlaybackConfig            _config;
    private CancellationTokenSource?  _cts;
    private Thread?                   _playbackThread;
    private Thread?                   _panicThread;
    private bool                      _disposed;

    public bool IsPlaying { get; private set; }

    public event Action? PlaybackStarted;
    public event Action? PlaybackStopped;

    public void Load(IReadOnlyList<InputEvent> events) => _events = events;

    public void Start(PlaybackConfig config)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsPlaying) return;
        if (_events.Count == 0) return;

        _config = config;
        _cts    = new CancellationTokenSource();

        // Panic monitor — polls GetAsyncKeyState at ~100 Hz on its own thread.
        // No Win32 message pump required; GetAsyncKeyState is state-based, not event-based.
        _panicThread = new Thread(() => PanicMonitorProc(_cts))
        {
            IsBackground = true,
            Name         = "ScriptrPanicMonitor"
        };

        _playbackThread = new Thread(() => PlaybackThreadProc(_cts.Token))
        {
            IsBackground = true,
            Name         = "ScriptrPlaybackThread",
            Priority     = ThreadPriority.AboveNormal
        };

        IsPlaying = true;
        PlaybackStarted?.Invoke();
        _panicThread.Start();
        _playbackThread.Start();
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    // ── Panic monitor ────────────────────────────────────────────────────────

    private static void PanicMonitorProc(CancellationTokenSource cts)
    {
        while (!cts.Token.IsCancellationRequested)
        {
            // High bit of GetAsyncKeyState return value = key physically held down.
            if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_PAUSE) & unchecked((short)0x8000)) != 0)
            {
                cts.Cancel();
                return;
            }
            Thread.Sleep(10);
        }
    }

    // ── Playback thread ──────────────────────────────────────────────────────

    private void PlaybackThreadProc(CancellationToken ct)
    {
        try
        {
            switch (_config.Mode)
            {
                case LoopMode.PlayOnce:
                    PlaySequence(ct);
                    break;

                case LoopMode.RepeatN:
                    for (int i = 0; i < _config.RepeatCount && !ct.IsCancellationRequested; i++)
                        PlaySequence(ct);
                    break;

                case LoopMode.Continuous:
                    while (!ct.IsCancellationRequested)
                        PlaySequence(ct);
                    break;
            }
        }
        finally
        {
            IsPlaying = false;
            PlaybackStopped?.Invoke();
        }
    }

    private void PlaySequence(CancellationToken ct)
    {
        long sequenceStart = Stopwatch.GetTimestamp();

        foreach (InputEvent evt in _events)
        {
            if (ct.IsCancellationRequested) return;

            // Scale the recorded timestamp by the speed multiplier.
            long targetUs = (long)(evt.TimestampUs / _config.SpeedMultiplier);
            WaitUntilUs(sequenceStart, targetUs, ct);

            if (ct.IsCancellationRequested) return;

            InjectEvent(evt);
        }
    }

    // ── High-resolution delay ────────────────────────────────────────────────

    private static void WaitUntilUs(long sequenceStart, long targetUs, CancellationToken ct)
    {
        while (true)
        {
            if (ct.IsCancellationRequested) return;

            long elapsedUs = TicksToMicroseconds(Stopwatch.GetTimestamp() - sequenceStart);
            long remainUs  = targetUs - elapsedUs;

            if (remainUs <= 0) return;

            if (remainUs > 2_000)
            {
                // Sleep for the bulk of the wait, leave ~2 ms for the spin phase.
                // Cap at 15 ms so the cancellation token is checked frequently.
                int sleepMs = (int)Math.Min((remainUs - 2_000) / 1_000, 15);
                if (sleepMs > 0)
                    Thread.Sleep(sleepMs);
            }
            else
            {
                // Sub-2 ms remainder: busy-spin to honour microsecond precision.
                Thread.SpinWait(20);
            }
        }
    }

    private static long TicksToMicroseconds(long ticks) =>
        (long)(ticks * 1_000_000.0 / Stopwatch.Frequency);

    // ── Input injection ──────────────────────────────────────────────────────

    private void InjectEvent(InputEvent evt)
    {
        if (evt.IsMouseEvent)
            InjectMouseEvent(evt);
        else if (evt.IsKeyboardEvent)
            InjectKeyboardEvent(evt);
    }

    private void InjectMouseEvent(InputEvent evt)
    {
        // Map [0,1] back to Win32 absolute units [0, 65535] spanning the virtual desktop.
        int  dx    = (int)Math.Round(evt.NormalizedX * 65535.0);
        int  dy    = (int)Math.Round(evt.NormalizedY * 65535.0);
        uint flags = NativeMethods.MOUSEEVENTF_ABSOLUTE | NativeMethods.MOUSEEVENTF_VIRTUALDESK;
        uint data  = 0;

        switch (evt.EventType)
        {
            case InputEventType.MouseMove:
                flags |= NativeMethods.MOUSEEVENTF_MOVE;
                break;

            case InputEventType.MouseDown:
                (flags, data) = ApplyButtonFlags(evt.Button, isDown: true, flags);
                break;

            case InputEventType.MouseUp:
                (flags, data) = ApplyButtonFlags(evt.Button, isDown: false, flags);
                break;

            case InputEventType.MouseWheel:
                flags |= NativeMethods.MOUSEEVENTF_WHEEL;
                // ScrollDelta is a signed int; reinterpret as uint for the DWORD field.
                data = (uint)(int)evt.ScrollDelta;
                break;

            default:
                return;
        }

        _inputBuffer[0] = new INPUT
        {
            Type = NativeMethods.INPUT_MOUSE,
            Data = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx          = dx,
                    dy          = dy,
                    mouseData   = data,
                    dwFlags     = flags,
                    time        = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        NativeMethods.SendInput(1, _inputBuffer, NativeMethods.InputSize);
    }

    private static (uint flags, uint data) ApplyButtonFlags(MouseButton btn, bool isDown, uint baseFlags) =>
        (btn, isDown) switch
        {
            (MouseButton.Left,   true)  => (baseFlags | NativeMethods.MOUSEEVENTF_LEFTDOWN,   0u),
            (MouseButton.Left,   false) => (baseFlags | NativeMethods.MOUSEEVENTF_LEFTUP,     0u),
            (MouseButton.Right,  true)  => (baseFlags | NativeMethods.MOUSEEVENTF_RIGHTDOWN,  0u),
            (MouseButton.Right,  false) => (baseFlags | NativeMethods.MOUSEEVENTF_RIGHTUP,    0u),
            (MouseButton.Middle, true)  => (baseFlags | NativeMethods.MOUSEEVENTF_MIDDLEDOWN, 0u),
            (MouseButton.Middle, false) => (baseFlags | NativeMethods.MOUSEEVENTF_MIDDLEUP,   0u),
            (MouseButton.X1,     true)  => (baseFlags | NativeMethods.MOUSEEVENTF_XDOWN,  (uint)NativeMethods.XBUTTON1),
            (MouseButton.X1,     false) => (baseFlags | NativeMethods.MOUSEEVENTF_XUP,    (uint)NativeMethods.XBUTTON1),
            (MouseButton.X2,     true)  => (baseFlags | NativeMethods.MOUSEEVENTF_XDOWN,  (uint)NativeMethods.XBUTTON2),
            (MouseButton.X2,     false) => (baseFlags | NativeMethods.MOUSEEVENTF_XUP,    (uint)NativeMethods.XBUTTON2),
            _                           => (baseFlags, 0u)
        };

    private void InjectKeyboardEvent(InputEvent evt)
    {
        // Use hardware scan code so game engines receive a real PS/2 scancode, not a VK.
        uint flags = NativeMethods.KEYEVENTF_SCANCODE;

        if (evt.EventType is InputEventType.KeyUp or InputEventType.SysKeyUp)
            flags |= NativeMethods.KEYEVENTF_KEYUP;

        // Replay the extended-key flag (e.g. right Ctrl, right Alt, numpad Enter).
        if ((evt.Flags & KeyFlags.Extended) != 0)
            flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;

        _inputBuffer[0] = new INPUT
        {
            Type = NativeMethods.INPUT_KEYBOARD,
            Data = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk         = 0,           // Must be 0 when KEYEVENTF_SCANCODE is set
                    wScan       = evt.ScanCode,
                    dwFlags     = flags,
                    time        = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        NativeMethods.SendInput(1, _inputBuffer, NativeMethods.InputSize);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        _playbackThread?.Join(TimeSpan.FromSeconds(3));
        _panicThread?.Join(TimeSpan.FromMilliseconds(200));
        _cts?.Dispose();
    }
}
