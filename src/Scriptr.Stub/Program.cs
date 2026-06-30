using Scriptr.Core;
using Scriptr.Core.Playback;
using Scriptr.Core.Serialization;

// Declare Per-Monitor DPI awareness before any screen metric calls.
Platform.InitDpiAwareness();

// Locate the embedded .rec payload appended to this executable.
string selfPath = Environment.ProcessPath
    ?? throw new InvalidOperationException("Cannot determine executable path.");

if (!MacroCompiler.TryReadEmbeddedPayload(selfPath, out byte[] recBytes))
{
    Console.Error.WriteLine("Scriptr Stub: no embedded macro payload found.");
    Console.Error.WriteLine("Use 'Scriptr.Cli --compile <input.rec> <output.exe>' to create a standalone macro.");
    return 1;
}

// Deserialize the .rec payload from memory — no disk I/O after this point.
List<Scriptr.Core.Events.InputEvent> events;
using (var ms = new MemoryStream(recBytes, writable: false))
    events = MacroSerializer.Deserialize(ms);

if (events.Count == 0)
{
    Console.Error.WriteLine("Scriptr Stub: embedded macro contains zero events.");
    return 1;
}

Console.WriteLine($"Scriptr Macro Player  |  {events.Count} events");
Console.WriteLine("Press Pause/Break to abort at any time.");
Console.WriteLine();

// Parse optional command-line arguments: [speed] [once|N|forever]
float speedMultiplier = 1.0f;
PlaybackConfig config = PlaybackConfig.Once();

if (args.Length >= 1 && float.TryParse(args[0], out float parsedSpeed) && parsedSpeed > 0f)
    speedMultiplier = parsedSpeed;

if (args.Length >= 2)
{
    config = args[1].ToLowerInvariant() switch
    {
        "forever" => PlaybackConfig.Forever(speedMultiplier),
        var s when int.TryParse(s, out int n) && n > 0 => PlaybackConfig.Repeat(n, speedMultiplier),
        _ => PlaybackConfig.Once(speedMultiplier)
    };
}
else
{
    config = PlaybackConfig.Once(speedMultiplier);
}

Console.WriteLine($"Mode: {config.Mode}  Speed: ×{config.SpeedMultiplier:0.##}");
Console.WriteLine();

using var engine = new PlaybackEngine();
engine.Load(events);

var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
engine.PlaybackStarted  += () => Console.WriteLine("▶ Playing...");
engine.PlaybackStopped  += () => done.TrySetResult();

engine.Start(config);
await done.Task;

Console.WriteLine("■ Done.");
return 0;
