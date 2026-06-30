# Scriptr

Scriptr is an ultra-lightweight, portable Windows desktop automation and macro recording utility. It enables users to record, configure, playback, and compile mouse and keyboard inputs via low-level global hardware hooks with zero external runtime dependencies.

---

## 1. Functional Requirements

### Core Automation Engine
* **Global Low-Level Hooking:** Implement background interceptors for global mouse movements (X/Y coordinates), click states (left, right, middle, scroll wheel), and keyboard actions.
* **Hardware Scan Codes:** Capture keyboard inputs using hardware scan codes rather than basic virtual key mapping to maintain strict compatibility with low-level game engines.
* **Deterministic Input Capture:** Drag-and-drop actions and camera panning tracking must capture fluid `MouseDown`, `MouseMove`, and `MouseUp` events sequentially.
* **Thread Isolation:** The hooking listeners and the playback execution player must run on isolated background threads to prevent the primary UI thread from freezing.

### Configuration & Playback Options
* **Independent Hotkey Binding:** Provide clean, modular modal configuration settings to map custom global hotkeys for:
  * Start / Stop Recording
  * Play / Pause Playback
* **Hotkey Omission Rule:** Prevent the engine from self-recording control hotkeys. For example, hitting the "Stop Recording" hotkey must be stripped from the event queue immediately.
* **Emergency Panic Button:** Reserve a dedicated high-priority interrupt key (e.g., `Pause/Break`) to instantly kill all active execution loops.
* **Loop Configuration:** Supported execution runtime paradigms:
  * `Integer Count`: Iterate exactly $N$ times and terminate.
  * `Continuous Playback`: Execute inside an infinite loop until interrupted by the user.
* **Variable Playback Speed:** Implement a responsive execution multiplier (1x, 2x, 5x, 10x, up to 100x) that divides event timestamp delays programmatically.
* **Resolution Independence:** Normalize mouse coordinates relative to virtual screen boundaries ($0.0$ to $1.0$) rather than raw pixels to ensure absolute macros map accurately across various display monitors and scaling settings (e.g., 125% DPI scaling).

### Portability & File Management
* **Custom Serialization:** Parse recorded event arrays into custom, space-efficient `.rec` files.
* **Standalone Compilation Engine:** Bundle macro event sequences alongside a stripped-down headless compilation runtime into a portable, independent `.exe` file.

---

## 2. Technical System Architecture

* **UI Layer:** Minimalist, highly scannable, compact interface pinned via an "Always on Top" system flag.
* **State Management (`CLAUDE.md`):** A strict synchronization manifest maintained dynamically throughout development to track implemented modules, system bugs, and current focus paradigms.

---

## 3. Development Workflow Constraints

* **Zero Code Placeholders:** All code outputs must be complete and production-ready. Structural shorthand comments like `// TODO: implement logic` are strictly unauthorized.
* **Iterative Milestone Plan:** Development must follow strict, isolated architectural phases:
  * **Phase 1:** Low-Level Input Hooking Engine (CLI Verification)
  * **Phase 2:** Time-Delayed Playback Execution Engine & Loop Modifiers
  * **Phase 3:** Serialization Engine (`.rec` parsing) & Compiler Pipeline
  * **Phase 4:** Compact GUI Integration & System Tray Core

---

## 4. State Manifest

### Stack
- Language: C# 12 / .NET 8 (`net8.0-windows`)
- No NuGet packages — pure BCL + Win32 P/Invoke
- Build: `dotnet build Scriptr.sln`
- Run harness: `dotnet run --project src/Scriptr.Cli`

### Completed — Phase 1
| Module | File | Status |
|--------|------|--------|
| Win32 hook structs | `src/Scriptr.Core/Native/NativeStructs.cs` | ✅ |
| Win32 hook P/Invoke | `src/Scriptr.Core/Native/NativeMethods.cs` | ✅ |
| `InputEventType` enum | `src/Scriptr.Core/Events/InputEventType.cs` | ✅ |
| `MouseButton` enum | `src/Scriptr.Core/Events/MouseButton.cs` | ✅ |
| `KeyFlags` enum | `src/Scriptr.Core/Events/KeyFlags.cs` | ✅ |
| `InputEvent` struct | `src/Scriptr.Core/Events/InputEvent.cs` | ✅ |
| `MouseHook` | `src/Scriptr.Core/Hooks/MouseHook.cs` | ✅ |
| `KeyboardHook` | `src/Scriptr.Core/Hooks/KeyboardHook.cs` | ✅ |
| `InputHookEngine` | `src/Scriptr.Core/Hooks/InputHookEngine.cs` | ✅ |
| `RecordingSession` | `src/Scriptr.Core/Recording/RecordingSession.cs` | ✅ |

### Completed — Phase 2
| Module | File | Status |
|--------|------|--------|
| `MOUSEINPUT`, `KEYBDINPUT`, `INPUTUNION`, `INPUT` | `src/Scriptr.Core/Native/NativeStructs.cs` | ✅ |
| `SendInput`, `GetAsyncKeyState`, MOUSEEVENTF/KEYEVENTF constants | `src/Scriptr.Core/Native/NativeMethods.cs` | ✅ |
| `LoopMode` enum | `src/Scriptr.Core/Playback/LoopMode.cs` | ✅ |
| `PlaybackConfig` struct | `src/Scriptr.Core/Playback/PlaybackConfig.cs` | ✅ |
| `PlaybackEngine` | `src/Scriptr.Core/Playback/PlaybackEngine.cs` | ✅ |
| CLI Phase 1+2 harness | `src/Scriptr.Cli/Program.cs` | ✅ |

### Architecture Notes — Phase 2
- `INPUT` struct: Sequential outer + Explicit inner union (`INPUTUNION`). On x64: 40 bytes. Size cached in `NativeMethods.InputSize`.
- Mouse replay: `MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK` with coords mapped `[0,1] → [0, 65535]`.
- Keyboard replay: `KEYEVENTF_SCANCODE` only — `wVk=0`, so game engines receive a raw PS/2 scan code.
- Timing: hybrid `Thread.Sleep` (bulk) + `Thread.SpinWait` (sub-2ms) for microsecond accuracy.
- Panic key: dedicated `PanicMonitorProc` thread polling `GetAsyncKeyState(VK_PAUSE)` at 100 Hz. Cancels the shared `CancellationTokenSource`.
- `PlaybackEngine._inputBuffer` is a pre-allocated `INPUT[1]` field — zero per-event heap allocations.

### Completed — Phase 3
| Module | File | Status |
|--------|------|--------|
| `MacroSerializer` | `src/Scriptr.Core/Serialization/MacroSerializer.cs` | ✅ |
| `MacroCompiler` | `src/Scriptr.Core/Serialization/MacroCompiler.cs` | ✅ |
| `Scriptr.Stub` project | `src/Scriptr.Stub/Scriptr.Stub.csproj` | ✅ |
| Stub player runtime | `src/Scriptr.Stub/Program.cs` | ✅ |
| CLI with --record/--play/--compile | `src/Scriptr.Cli/Program.cs` | ✅ |

### Architecture Notes — Phase 3

**.rec format (27 bytes per event, no padding):**
- Header: `SCRI` magic (4) + version uint16 (2) + reserved uint16 (2) + count uint32 (4) = 12 bytes
- Events: sequential 27-byte blocks, field order mirrors `InputEvent` struct declaration
- All integers written little-endian; floats written as IEEE 754 via `BitConverter.SingleToUInt32Bits`

**Serializer design:**
- `ArrayPool<byte>.Shared` for zero-GC batched I/O (256 KB chunks)
- Manual LE primitives (no `BinaryWriter`) — avoids per-field stream-write syscall overhead
- `stream.ReadExactly(Span<byte>)` for header; rented buffer for event batches
- Forward-compatible: reserved bytes silently skipped on read; unknown versions throw

**Compiler pipeline (PE-append technique):**
- `Scriptr.Stub.exe` is a self-contained SelfContained/PublishTrimmed win-x64 executable
- `--compile` copies the stub, then appends: `[.rec bytes][8-byte length LE][16-byte sentinel "SCRIPTR_EXE_TAIL"]`
- Windows PE loader ignores bytes after the last section → appended data is invisible to the OS
- The stub reads backwards from `Environment.ProcessPath` at startup to find the payload
- Resulting `.exe` is fully standalone: no .NET runtime, no Scriptr.Cli, no .rec file required

**Deployment for `--compile`:**
- `Scriptr.Stub.exe` must be in the same directory as `Scriptr.Cli.exe`
- Publish both with `dotnet publish`; the stub is located by `Path.GetDirectoryName(Environment.ProcessPath)`

### Completed — Phase 4
| Module | File | Status |
|--------|------|--------|
| Hotkey P/Invokes + constants | `src/Scriptr.Core/Native/NativeMethods.cs` | ✅ |
| Public hotkey bridge | `src/Scriptr.Core/Platform.cs` | ✅ |
| Thread-safe `EventCount` | `src/Scriptr.Core/Recording/RecordingSession.cs` | ✅ |
| WinForms project | `src/Scriptr.Gui/Scriptr.Gui.csproj` | ✅ |
| Entry point | `src/Scriptr.Gui/Program.cs` | ✅ |
| Shared state | `src/Scriptr.Gui/AppState.cs` | ✅ |
| Hotkey binding model | `src/Scriptr.Gui/HotkeyBinding.cs` | ✅ |
| RegisterHotKey engine | `src/Scriptr.Gui/HotkeyEngine.cs` | ✅ |
| Main window | `src/Scriptr.Gui/MainForm.cs` | ✅ |
| Settings dialog | `src/Scriptr.Gui/SettingsForm.cs` | ✅ |

### Architecture Notes — Phase 4

**`HotkeyEngine`:** Wraps `Platform.RegisterHotKey`/`UnregisterHotKey`. Installed in `MainForm.OnLoad` (requires HWND). Dispatched from `WndProc` override via `ProcessMessage(ref Message m)` — all callbacks fire on the UI thread.

**`HotkeyBinding`:** Stores `(Keys Key, Keys Modifiers)`. `Win32Modifiers` always ORs in `MOD_NOREPEAT (0x4000)` to prevent auto-repeat floods. `DisplayText` formats human-readable labels ("Ctrl+F10").

**`MainForm` state machine:** `enum AppPhase { Idle, Recording, Playing }`. `SynchronizeUiState()` gates button enabled states and updates text/color. Status refresh timer fires every 150 ms; safe-captures `_recording` reference to avoid null-race during async stop.

**Threading rules:**
- `RecordingSession.StopAsync()` is awaited on a thread pool thread (`.ConfigureAwait(false)` inside); UI mutations happen via `BeginInvokeIfAlive(Action)` which checks `IsHandleCreated && !IsDisposed` before marshalling.
- `_recording = null` and `_phase = Idle` are set inside the `BeginInvoke` lambda to ensure they're always set on the UI thread — the timer (also UI thread) never races against them.
- `PlaybackEngine.PlaybackStopped` fires on the playback thread → marshalled to `OnPlaybackFinished` via `BeginInvokeIfAlive`.

**TopMost + dialog workaround:** `TopMost = false` before every modal dialog, restored in `finally`. Without this, dialogs appear behind the always-on-top window.

**Tray behavior:** X button hides to tray (cancel close); `_reallyClosing = true` flag set by tray Exit menu forces a real close on next `OnFormClosing`.

**`Platform` facade additions:** Public `RegisterHotKey`/`UnregisterHotKey` wrappers + `WM_HOTKEY` and `MOD_*` constants added so `Scriptr.Gui` never accesses `internal NativeMethods`.

**`RecordingSession.EventCount`:** `Volatile.Read(ref _eventCount)` backed by `Interlocked.Increment` in `ConsumeAsync`. The UI timer reads this without touching `List<T>.Count` across threads.

### Completed — Phase 5
| Module | File | Status |
|--------|------|--------|
| Unicode toolbar icons (📁💾🔴▶🛠⚙) | `src/Scriptr.Gui/MainForm.cs` | ✅ |
| ToolTip descriptors on all buttons | `src/Scriptr.Gui/MainForm.cs` | ✅ |
| App icon embedded resource + runtime loader | `src/Scriptr.Gui/Program.cs` | ✅ |
| `ApplicationIcon` + Release publish flags | `src/Scriptr.Gui/Scriptr.Gui.csproj` | ✅ |
| Release publish flags, PublishTrimmed=false | `src/Scriptr.Stub/Scriptr.Stub.csproj` | ✅ |
| Distribution build script | `build_release.ps1` | ✅ |

### Architecture Notes — Phase 5

**Icon pipeline:**
- `build_release.ps1` converts `src/Scriptr.Gui/Assets/scriptr_icon.png` → `scriptr_icon.ico` (16/32/48 px frames, 32bpp DIB with alpha-channel AND mask) before `dotnet publish` runs.
- `<ApplicationIcon>` in `Scriptr.Gui.csproj` is wrapped in `Condition="Exists(...)"` so Debug builds succeed without the ICO file present.
- `<EmbeddedResource LogicalName="Scriptr.Assets.scriptr_icon.ico">` embeds the ICO into the assembly; `Program.LoadAppIcon()` reads it via `GetManifestResourceStream` → `new Icon(stream, 32, 32)`. Falls back to `SystemIcons.Application` if stream is null (Debug without the ICO).
- `_trayIcon.Icon` and `Form.Icon` both receive the same `Icon` instance, owned by `Program.Main` for the full app lifetime.

**Toolbar rendering:**
- `_strip.Font = new Font("Segoe UI Emoji", 10f)` ensures color emoji render correctly on Windows 10/11 via Segoe UI Emoji.
- Record button: idle = `"🔴"`, recording = `"⏹"` (monochrome, so ForeColor blink Red ↔ DarkRed remains visible).
- Play button: idle = `"▶"`, playing = `"⏹"`.

**Release build:**
- `Scriptr.Stub.csproj`: removed `<TrimmerRootAssembly>` (no longer needed since `PublishTrimmed=false`).
- Both projects use `PublishSingleFile=true`, `SelfContained=true`, `win-x64`, `PublishTrimmed=false` under `Condition="'$(Configuration)' == 'Release'"`.
- `build_release.ps1` publishes Stub first (so `Scriptr.Stub.exe` is available for the compile feature), then Gui; drops both into `dist\`.

### Known Issues
- None

### Build
```
dotnet build Scriptr.sln             # all projects — 0 errors, 0 warnings
dotnet run --project src/Scriptr.Cli  # CLI harness (Phases 1–3)
dotnet run --project src/Scriptr.Gui  # GUI (Phase 4+5)
.\build_release.ps1                   # Phase 5: generate ICO + publish to dist\
```
Place `scriptr_icon.png` in `src\Scriptr.Gui\Assets\` before running `build_release.ps1`.
SDK: .NET 8.0.422 installed via winget on 2026-06-29.

**`Platform` facade (`src/Scriptr.Core/Platform.cs`):**  
Public surface for `NativeMethods` (which stays `internal`). Exposes DPI initialization, virtual screen metrics, and hotkey registration — all entry-point projects use this instead of reaching into internals.
