# Scriptr

**Lightweight Windows macro recorder and compiler. No installation, no runtime, no dependencies.**

Record mouse and keyboard inputs, play them back at any speed, and export them as fully standalone `.exe` files that run on any Windows machine — even ones that have never had Scriptr installed.

---

## Download

Grab the latest release from the [Releases page](https://github.com/Hermano727/Scriptr/releases).

| File | Description |
|---|---|
| `Scriptr.exe` | The main application |
| `Scriptr.Stub.exe` | Required alongside `Scriptr.exe` to use the Compile feature |

Place both files in the same folder. Double-click `Scriptr.exe` to launch.

---

## Features

- **Global input capture** — records mouse position, clicks, scroll, and keyboard via low-level Windows hooks
- **Hardware scan codes** — keyboard events use PS/2 scan codes, not virtual keys, for compatibility with games and low-level applications
- **Variable playback speed** — replay at 1×, 2×, 5×, 10×, up to 100× using the Settings menu
- **Loop control** — right-click the Play button to set repeat count or loop forever
- **Hotkey driven** — fully controllable without touching the window; all binds are remappable
- **Panic stop** — press `Pause/Break` at any time to immediately kill an active playback
- **Portable `.rec` format** — save and load macro recordings as compact binary files
- **Compile to `.exe`** — bundle any macro into a self-contained executable (see below)
- **System tray** — closes to tray, stays out of the way

---

## Usage

### Recording a macro

1. Click **🔴 Record** or press your record hotkey (default: `F10`)
2. Perform the actions you want to capture — mouse movements, clicks, and keystrokes are all recorded
3. Press the record hotkey again to stop. A red hint below the toolbar shows the key while recording
4. Click **💾 Save** to save the macro as a `.rec` file

### Playing a macro back

1. Click **📁 Open** to load a `.rec` file, or use one recorded in the current session
2. Click **▶ Play** or press your play hotkey (default: `F8`) to start playback
3. Press the play hotkey again (or `Pause/Break`) to stop early

### Setting loop count

Right-click the **▶ Play** button before starting to choose how many times the macro runs:

- **Play once** — runs through one time and stops
- **Repeat N×** — preset counts (5, 10, 50, 100) or enter a custom number
- **Loop forever** — runs until you press the abort hotkey or `Pause/Break`

### Hotkeys

| Action | Default | Notes |
|---|---|---|
| Start / Stop recording | `F10` | Remappable in ⚙ Settings |
| Start / Abort playback | `F8` | Remappable in ⚙ Settings |
| Emergency stop | `Pause/Break` | Hard-coded, always active during playback |

Open **⚙ Settings** to change hotkeys and playback speed.

---

## Compiling a macro to a standalone `.exe`

The **🛠 Compile** feature lets you package any `.rec` file into a self-contained Windows executable. The resulting `.exe`:

- Plays the macro automatically when launched
- Requires **no installation** and **no .NET runtime** on the target machine
- Has no dependency on Scriptr itself

**This is useful when you want to:**
- Run a macro on a machine that doesn't have Scriptr installed
- Give someone an automated task as a single file they can just double-click
- Schedule a macro via Windows Task Scheduler

### How to compile

1. Click **🛠 Compile** in the toolbar
2. Select the source `.rec` file in the file picker
3. Choose where to save the output `.exe`
4. The compiled executable is created immediately — run it on any Windows x64 machine

> `Scriptr.Stub.exe` must be in the same folder as `Scriptr.exe` for the Compile button to work. If you only downloaded `Scriptr.exe`, grab `Scriptr.Stub.exe` from the same release.

---

## Toolbar reference

| Button | Action |
|---|---|
| 📁 Open | Load a `.rec` macro file |
| 💾 Save | Save the current recording to a `.rec` file |
| 🔴 Record | Start recording (click again or press hotkey to stop) |
| ▶ Play | Start playback (right-click to set loop count) |
| ⚙ Settings | Configure hotkeys and playback speed |
| 🛠 Compile | Export a `.rec` file as a standalone `.exe` |

---

## Building from source

**Requirements:** .NET 8 SDK (`winget install Microsoft.DotNet.SDK.8`)

```powershell
git clone https://github.com/Hermano727/Scriptr.git
cd Scriptr
dotnet build Scriptr.sln
dotnet run --project src/Scriptr.Gui
```

To produce optimized release binaries in `dist\`:

```powershell
# Optional: place a 32x32+ PNG at src/Scriptr.Gui/Assets/scriptr_icon.png first
.\build_release.ps1
```

Output: `dist\Scriptr.exe` + `dist\Scriptr.Stub.exe` — both self-contained, no .NET runtime required on the target machine.

---

## How it works

Scriptr uses Windows low-level hooks (`WH_MOUSE_LL`, `WH_KEYBOARD_LL`) to capture input globally while recording. On playback it replays events through `SendInput` with `MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK` for resolution-independent mouse coordinates and `KEYEVENTF_SCANCODE` for hardware-level keyboard fidelity.

The `.rec` format is a compact binary (27 bytes per event). The Compile pipeline appends the recording to a stripped headless runtime (`Scriptr.Stub.exe`) — the Windows PE loader ignores appended data, so the result is a valid `.exe` that self-extracts and replays on launch.

---

## Platform

- Windows 10 / 11 (x64)
- No .NET runtime required for compiled macros or release builds
- Source builds require .NET 8 SDK

---

## License

MIT
