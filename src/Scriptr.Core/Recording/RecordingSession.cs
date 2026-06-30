using Scriptr.Core.Events;
using Scriptr.Core.Hooks;

namespace Scriptr.Core.Recording;

public sealed class RecordingSession : IDisposable
{
    private readonly InputHookEngine _engine;
    private readonly List<InputEvent> _events = new();
    private CancellationTokenSource? _cts;
    private Task? _consumerTask;
    private bool _disposed;
    private int _eventCount;

    public IReadOnlyList<InputEvent> Events => _events;
    public bool IsRecording { get; private set; }
    public int EventCount => Volatile.Read(ref _eventCount);

    public event Action<ushort>? ControlKeyPressed;

    public RecordingSession(Func<ushort, bool>? isControlKey = null)
    {
        _engine = new InputHookEngine(isControlKey);
        _engine.ControlKeyPressed += vk => ControlKeyPressed?.Invoke(vk);
    }

    public void Start()
    {
        if (IsRecording) return;
        _events.Clear();
        _eventCount = 0;
        _cts = new CancellationTokenSource();
        IsRecording = true;
        _engine.Start();
        _consumerTask = ConsumeAsync(_cts.Token);
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        try
        {
            await foreach (InputEvent evt in _engine.Events.ReadAllAsync(ct).ConfigureAwait(false))
            {
                _events.Add(evt);
                Interlocked.Increment(ref _eventCount);
            }
        }
        catch (OperationCanceledException) { }
    }

    public async Task StopAsync()
    {
        if (!IsRecording) return;
        IsRecording = false;
        _engine.Stop();
        _cts?.Cancel();
        if (_consumerTask is not null)
            await _consumerTask.ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _engine.Dispose();
        _cts?.Dispose();
    }
}
