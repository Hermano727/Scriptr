using Scriptr.Core;
using Scriptr.Core.Events;
using Scriptr.Core.Playback;
using Scriptr.Core.Recording;
using Scriptr.Core.Serialization;

// Must precede all GetSystemMetrics / hook-install calls.
Platform.InitDpiAwareness();

return args.Length switch
{
    0 => await RunInteractiveAsync(),
    _ => await RunCommandAsync(args)
};

// ── Command dispatch ──────────────────────────────────────────────────────────

static async Task<int> RunCommandAsync(string[] args)
{
    switch (args[0].ToLowerInvariant())
    {
        case "--record" when args.Length == 2:
            return await RunRecordAsync(args[1]);

        case "--play" when args.Length == 2:
            return await RunPlayAsync(args[1], PlaybackConfig.Once());

        case "--play" when args.Length >= 3:
            return await RunPlayAsync(args[1], ParseConfigFromArgs(args, startIndex: 2));

        case "--compile" when args.Length == 3:
            return await RunCompileAsync(args[1], args[2]);

        default:
            PrintUsage();
            return 1;
    }
}

// ── --record <output.rec> ─────────────────────────────────────────────────────

static async Task<int> RunRecordAsync(string outPath)
{
    const ushort VK_ESCAPE = 0x1B;
    using var session = new RecordingSession(vk => vk == VK_ESCAPE);

    var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    session.ControlKeyPressed += _ => stopped.TrySetResult();

    session.Start();
    Console.WriteLine($"● Recording → {outPath}");
    Console.WriteLine("  Press ESC to stop (30-second timeout).");

    await Task.WhenAny(stopped.Task, Task.Delay(TimeSpan.FromSeconds(30)));
    await session.StopAsync();

    IReadOnlyList<InputEvent> events = session.Events;
    Console.WriteLine($"  Captured {events.Count} events.");

    if (events.Count == 0)
    {
        Console.Error.WriteLine("No events recorded — file not written.");
        return 1;
    }

    await using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write,
        FileShare.None, bufferSize: 65536, useAsync: true);
    MacroSerializer.Serialize(fs, events);

    long fileSize = new FileInfo(outPath).Length;
    Console.WriteLine($"  Saved: {outPath}  ({fileSize:N0} bytes)");
    return 0;
}

// ── --play <input.rec> [speed] [once|N|forever] ───────────────────────────────

static async Task<int> RunPlayAsync(string recPath, PlaybackConfig config)
{
    if (!File.Exists(recPath))
    {
        Console.Error.WriteLine($"File not found: {recPath}");
        return 1;
    }

    List<InputEvent> events;
    await using (var fs = new FileStream(recPath, FileMode.Open, FileAccess.Read,
        FileShare.Read, bufferSize: 65536, useAsync: true))
        events = MacroSerializer.Deserialize(fs);

    Console.WriteLine($"▶ Playing {recPath}  ({events.Count} events)");
    Console.WriteLine($"  Mode: {config.Mode}  Speed: ×{config.SpeedMultiplier:0.##}");
    Console.WriteLine("  Press Pause/Break to abort.");

    using var engine = new PlaybackEngine();
    engine.Load(events);

    var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    engine.PlaybackStopped += () => done.TrySetResult();
    engine.Start(config);

    await done.Task;
    Console.WriteLine("■ Playback complete.");
    return 0;
}

// ── --compile <input.rec> <output.exe> ───────────────────────────────────────

static async Task<int> RunCompileAsync(string recPath, string outputExePath)
{
    string? cliDir = Path.GetDirectoryName(Environment.ProcessPath);
    string stubPath = Path.Combine(cliDir ?? ".", "Scriptr.Stub.exe");

    Console.WriteLine($"Compiling: {recPath} → {outputExePath}");
    Console.WriteLine($"Stub:      {stubPath}");

    try
    {
        await MacroCompiler.CompileAsync(recPath, stubPath, outputExePath);
        long outputSize = new FileInfo(outputExePath).Length;
        Console.WriteLine($"Done.  Output: {outputExePath}  ({outputSize:N0} bytes)");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Compile failed: {ex.Message}");
        return 1;
    }
}

// ── Interactive mode (no args) ────────────────────────────────────────────────

static async Task<int> RunInteractiveAsync()
{
    var (vsLeft, vsTop, vsWidth, vsHeight) = Platform.GetVirtualScreen();
    Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
    Console.WriteLine("║  Scriptr  — Interactive Harness (Phases 1–3)            ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
    Console.WriteLine($"Virtual Screen  : ({vsLeft}, {vsTop})  {vsWidth}×{vsHeight} px");
    Console.WriteLine("Stop Recording  : ESC          (stripped from event queue)");
    Console.WriteLine("Panic Key       : Pause/Break  (cancels active playback)");
    Console.WriteLine();

    const ushort VK_ESCAPE = 0x1B;
    using var session = new RecordingSession(vk => vk == VK_ESCAPE);

    var recordStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    session.ControlKeyPressed += _ => recordStopped.TrySetResult();

    session.Start();
    Console.WriteLine("● Recording...  (ESC to stop, 30-second timeout)");
    Console.WriteLine();

    await Task.WhenAny(recordStopped.Task, Task.Delay(TimeSpan.FromSeconds(30)));
    await session.StopAsync();

    IReadOnlyList<InputEvent> events = session.Events;
    int mouseCount    = events.Count(e => e.IsMouseEvent);
    int keyboardCount = events.Count(e => e.IsKeyboardEvent);

    Console.WriteLine();
    Console.WriteLine($"{"µs offset",-14}  {"Type",-13}  Details");
    Console.WriteLine(new string('─', 64));
    foreach (InputEvent evt in events)
        Console.WriteLine($"{evt.TimestampUs,-14}  {evt.EventType,-13}  {FormatDetails(evt)}");
    Console.WriteLine(new string('─', 64));
    Console.WriteLine($"Captured: {events.Count}   Mouse: {mouseCount}   Keyboard: {keyboardCount}");

    if (events.Count == 0) return 0;

    Console.WriteLine();
    Console.Write("Save to .rec file? (path or ENTER to skip): ");
    string? savePath = Console.ReadLine()?.Trim();
    if (!string.IsNullOrEmpty(savePath))
    {
        await using var fs = new FileStream(savePath, FileMode.Create, FileAccess.Write,
            FileShare.None, bufferSize: 65536, useAsync: true);
        MacroSerializer.Serialize(fs, events);
        Console.WriteLine($"Saved {events.Count} events → {savePath}");
    }

    Console.WriteLine();
    Console.WriteLine("── Playback ─────────────────────────────────────────────────");
    Console.Write("Speed [1/2/5/10/100, default=1]: ");
    float speed = ParseSpeed(Console.ReadLine()?.Trim());
    Console.Write("Loop  [once/N/forever, default=once]: ");
    PlaybackConfig config = ParseLoopConfig(Console.ReadLine()?.Trim(), speed);

    Console.WriteLine($"Config: {config.Mode}  ×{config.SpeedMultiplier:0.##}" +
                      (config.Mode == LoopMode.RepeatN ? $"  ({config.RepeatCount}×)" : ""));
    Console.WriteLine("Press ENTER to play, Pause/Break to abort.");
    Console.ReadLine();

    using var engine = new PlaybackEngine();
    engine.Load(events);

    var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    engine.PlaybackStarted += () => Console.WriteLine("▶ Playback running...");
    engine.PlaybackStopped += () => done.TrySetResult();
    engine.Start(config);

    await done.Task;
    Console.WriteLine("■ Playback complete.");
    return 0;
}

// ── Helpers ──────────────────────────────────────────────────────────────────

static string FormatDetails(InputEvent evt) => evt.EventType switch
{
    InputEventType.MouseMove =>
        $"({evt.NormalizedX:F4}, {evt.NormalizedY:F4})",
    InputEventType.MouseDown or InputEventType.MouseUp =>
        $"{evt.Button,-7} ({evt.NormalizedX:F4}, {evt.NormalizedY:F4})",
    InputEventType.MouseWheel =>
        $"delta={evt.ScrollDelta:+#;-#;0}",
    InputEventType.KeyDown or InputEventType.KeyUp =>
        $"VK=0x{evt.VirtualKey:X2}  SC=0x{evt.ScanCode:X4}  [{evt.Flags}]",
    InputEventType.SysKeyDown or InputEventType.SysKeyUp =>
        $"VK=0x{evt.VirtualKey:X2}  SC=0x{evt.ScanCode:X4}  [sys,{evt.Flags}]",
    _ => string.Empty
};

static float ParseSpeed(string? s) => s switch
{
    "2" => 2f, "5" => 5f, "10" => 10f, "100" => 100f, _ => 1f
};

// Used by the interactive flow — takes a single string token.
static PlaybackConfig ParseLoopConfig(string? mode, float speed) => mode switch
{
    null or "" or "once" => PlaybackConfig.Once(speed),
    "forever"            => PlaybackConfig.Forever(speed),
    _ when int.TryParse(mode, out int n) && n > 0 => PlaybackConfig.Repeat(n, speed),
    _                    => PlaybackConfig.Once(speed)
};

// Used by the --play command — reads speed and mode from positional args.
static PlaybackConfig ParseConfigFromArgs(string[] args, int startIndex)
{
    float speed = args.Length > startIndex &&
                  float.TryParse(args[startIndex], out float s) && s > 0f ? s : 1f;
    string? mode = args.Length > startIndex + 1 ? args[startIndex + 1] : null;
    return ParseLoopConfig(mode, speed);
}

static void PrintUsage()
{
    Console.WriteLine("Scriptr.Cli — Usage:");
    Console.WriteLine("  (no args)                           Interactive record → play session");
    Console.WriteLine("  --record <output.rec>               Record and save to .rec file");
    Console.WriteLine("  --play   <input.rec> [speed] [mode] Play a .rec file");
    Console.WriteLine("  --compile <input.rec> <output.exe>  Bundle macro into standalone .exe");
    Console.WriteLine();
    Console.WriteLine("  speed : 1 | 2 | 5 | 10 | 100  (default 1)");
    Console.WriteLine("  mode  : once | N | forever      (default once)");
}
