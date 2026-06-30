using Scriptr.Core.Playback;
using Scriptr.Core.Recording;
using Scriptr.Core.Serialization;

namespace Scriptr.Gui;

internal sealed class MainForm : Form
{
    private const int HK_RECORD = 1;
    private const int HK_ABORT  = 2;

    private readonly AppState _state;
    private readonly Icon     _appIcon;
    private HotkeyEngine?     _hotkeys;
    private RecordingSession? _recording;
    private PlaybackEngine?   _playback;
    private bool _reallyClosing;
    private bool _blinkState;

    private enum AppPhase { Idle, Recording, Playing }
    private AppPhase _phase = AppPhase.Idle;

    // Controls
    private ToolStrip            _strip       = null!;
    private ToolStripButton      _btnOpen     = null!;
    private ToolStripButton      _btnSave     = null!;
    private ToolStripButton      _btnRecord   = null!;
    private ToolStripButton      _btnPlay     = null!;
    private ToolStripButton      _btnCompile  = null!;
    private ToolStripButton      _btnSettings = null!;
    private StatusStrip          _statusStrip = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private NotifyIcon           _trayIcon    = null!;
    private System.Windows.Forms.Timer _timer = null!;

    public MainForm(AppState state, Icon appIcon)
    {
        _state   = state;
        _appIcon = appIcon;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text            = "Scriptr";
        Icon            = _appIcon;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        TopMost         = true;
        StartPosition   = FormStartPosition.CenterScreen;
        ClientSize      = new Size(510, 59);
        MinimizeBox     = true;
        MaximizeBox     = false;

        // ── ToolStrip ────────────────────────────────────────────────────────
        _strip = new ToolStrip
        {
            Dock      = DockStyle.Top,
            Height    = 36,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding   = new Padding(4, 2, 4, 2),
            Font      = new Font("Segoe UI Emoji", 10f)
        };

        _btnOpen     = MakeBtn("📁 Open",    "Open Macro File (.rec)",                                   BtnOpen_Click);
        _btnSave     = MakeBtn("💾 Save",    "Save Macro File (.rec)",                                   BtnSave_Click);
        _btnRecord   = MakeBtn("🔴 Record",  $"Toggle Recording  ({_state.RecordHotkey.DisplayText})",   BtnRecord_Click);
        _btnPlay     = MakeBtn("▶ Play",     $"Toggle Playback Loop  ({_state.AbortHotkey.DisplayText})", BtnPlay_Click);
        _btnCompile  = MakeBtn("🛠 Compile", "Compile to Standalone Executable (.exe)",                  BtnCompile_Click);
        _btnSettings = MakeBtn("⚙ Settings", "Configure Settings & Hotkeys",                             BtnSettings_Click);

        _strip.Items.AddRange([
            _btnOpen, _btnSave,
            new ToolStripSeparator(),
            _btnRecord, _btnPlay,
            new ToolStripSeparator(),
            _btnCompile, _btnSettings
        ]);

        // ── StatusStrip ──────────────────────────────────────────────────────
        _statusStrip = new StatusStrip { SizingGrip = false };
        _statusLabel = new ToolStripStatusLabel("Idle")
        {
            Spring    = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _statusStrip.Items.Add(_statusLabel);

        // ── Tray icon ────────────────────────────────────────────────────────
        _trayIcon = new NotifyIcon
        {
            Text    = "Scriptr",
            Icon    = _appIcon,
            Visible = true
        };
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Show / Hide",  null, (_, _) => ToggleVisibility());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Record / Stop", null, (_, _) => ToggleRecord());
        trayMenu.Items.Add("Play / Abort",  null, (_, _) => TogglePlayback());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Exit", null, TrayExit_Click);
        _trayIcon.ContextMenuStrip = trayMenu;
        _trayIcon.DoubleClick += (_, _) => ToggleVisibility();

        // ── Status refresh timer ─────────────────────────────────────────────
        _timer = new System.Windows.Forms.Timer { Interval = 150 };
        _timer.Tick += Timer_Tick;
        _timer.Start();

        Controls.Add(_strip);
        Controls.Add(_statusStrip);

        ResumeLayout(false);
        PerformLayout();
    }

    private static ToolStripButton MakeBtn(string text, string tooltip, EventHandler click)
    {
        var btn = new ToolStripButton(text)
        {
            ToolTipText  = tooltip,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            AutoSize     = true,
            Padding      = new Padding(6, 2, 6, 2)
        };
        btn.Click += click;
        return btn;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _hotkeys = new HotkeyEngine(Handle);
        ApplyHotkeys(_state.RecordHotkey, _state.AbortHotkey);
        SynchronizeUiState();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_reallyClosing && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            _trayIcon.ShowBalloonTip(2000, "Scriptr",
                "Scriptr is still running. Right-click the tray icon to exit.", ToolTipIcon.Info);
            return;
        }
        StopAllActive();
        _hotkeys?.Dispose();
        _timer.Stop();
        _timer.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (_hotkeys?.ProcessMessage(ref m) == true) return;
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _recording?.Dispose();
            _playback?.Dispose();
        }
        base.Dispose(disposing);
    }

    // ── Hotkeys ───────────────────────────────────────────────────────────────

    private void ApplyHotkeys(HotkeyBinding record, HotkeyBinding abort)
    {
        _hotkeys!.Register(HK_RECORD, record, ToggleRecord);
        _hotkeys!.Register(HK_ABORT,  abort,  AbortPlayback);
        _btnRecord.ToolTipText = $"Toggle Recording  ({record.DisplayText})";
        _btnPlay.ToolTipText   = $"Toggle Playback Loop  ({abort.DisplayText})";
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    private void BtnOpen_Click(object? sender, EventArgs e)
    {
        if (_phase != AppPhase.Idle) return;
        TopMost = false;
        try
        {
            using var dlg = new OpenFileDialog
            {
                Title  = "Open Macro Recording",
                Filter = "Scriptr Macro (*.rec)|*.rec|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                LoadFile(dlg.FileName);
        }
        finally { TopMost = true; }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        if (_phase != AppPhase.Idle || !_state.HasEvents) return;
        if (_state.FilePath is not null) { SaveToFile(_state.FilePath); return; }
        SaveAs();
    }

    private void BtnRecord_Click(object? sender, EventArgs e)  => ToggleRecord();
    private void BtnPlay_Click(object? sender, EventArgs e)     => TogglePlayback();

    private async void BtnCompile_Click(object? sender, EventArgs e)
    {
        if (_phase != AppPhase.Idle) return;

        string? recSource = null;
        string? outExe    = null;

        TopMost = false;
        try
        {
            using var openDlg = new OpenFileDialog
            {
                Title  = "Select source .rec file",
                Filter = "Scriptr Macro (*.rec)|*.rec"
            };
            if (openDlg.ShowDialog(this) != DialogResult.OK) return;
            recSource = openDlg.FileName;

            using var saveDlg = new SaveFileDialog
            {
                Title      = "Save compiled macro",
                Filter     = "Executable (*.exe)|*.exe",
                FileName   = Path.GetFileNameWithoutExtension(recSource) + ".exe",
                DefaultExt = "exe"
            };
            if (saveDlg.ShowDialog(this) != DialogResult.OK) return;
            outExe = saveDlg.FileName;
        }
        finally { TopMost = true; }

        string? cliDir   = Path.GetDirectoryName(Environment.ProcessPath);
        string  stubPath = Path.Combine(cliDir ?? ".", "Scriptr.Stub.exe");

        if (!File.Exists(stubPath))
        {
            MessageBox.Show(this,
                $"Scriptr.Stub.exe not found at:\n{stubPath}\n\n" +
                "Publish both Scriptr and Scriptr.Stub to the same directory.",
                "Compile Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        SetStatus($"🛠 Compiling {Path.GetFileName(recSource)}...");
        _btnCompile.Enabled = false;
        try
        {
            await MacroCompiler.CompileAsync(recSource, stubPath, outExe!);
            long size = new FileInfo(outExe!).Length;
            SetStatus($"🛠 Compiled — {Path.GetFileName(outExe)}  ({size:N0} bytes)");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Compile failed:\n{ex.Message}",
                "Compile Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Idle");
        }
        finally { _btnCompile.Enabled = true; }
    }

    private void BtnSettings_Click(object? sender, EventArgs e)
    {
        if (_phase != AppPhase.Idle) return;
        TopMost = false;
        try
        {
            using var form = new SettingsForm(_state.PlayConfig, _state.RecordHotkey, _state.AbortHotkey);
            if (form.ShowDialog(this) != DialogResult.OK) return;
            _state.PlayConfig   = form.ResultPlayConfig;
            _state.RecordHotkey = form.ResultRecordHotkey;
            _state.AbortHotkey  = form.ResultAbortHotkey;
            ApplyHotkeys(_state.RecordHotkey, _state.AbortHotkey);
        }
        finally { TopMost = true; }
    }

    private void TrayExit_Click(object? sender, EventArgs e)
    {
        _reallyClosing = true;
        Close();
    }

    // ── Recording ─────────────────────────────────────────────────────────────

    private void ToggleRecord()
    {
        if      (_phase == AppPhase.Idle)      StartRecording();
        else if (_phase == AppPhase.Recording) _ = StopRecordingAsync();
    }

    private void StartRecording()
    {
        _recording = new RecordingSession(isControlKey: null);
        _recording.Start();
        _phase = AppPhase.Recording;
        SynchronizeUiState();
        SetStatus("🔴 Recording — 0 events");
    }

    private async Task StopRecordingAsync()
    {
        if (_recording is null) return;
        var session = _recording;
        try { await session.StopAsync().ConfigureAwait(false); }
        catch { /* stop errors are non-fatal */ }

        var events = session.Events.ToList();

        BeginInvokeIfAlive(() =>
        {
            session.Dispose();
            _recording = null;
            _state.Events = events;
            _phase = AppPhase.Idle;
            SynchronizeUiState();
            SetStatus(events.Count > 0
                ? $"Recorded {events.Count:N0} events"
                : "Idle");
        });
    }

    // ── Playback ──────────────────────────────────────────────────────────────

    private void TogglePlayback()
    {
        if      (_phase == AppPhase.Idle && _state.HasEvents) StartPlayback();
        else if (_phase == AppPhase.Playing)                  StopPlayback();
    }

    private void AbortPlayback()
    {
        if (_phase == AppPhase.Playing) StopPlayback();
    }

    private void StartPlayback()
    {
        _playback = new PlaybackEngine();
        _playback.Load(_state.Events);
        _playback.PlaybackStopped += OnPlaybackStopped;
        _playback.Start(_state.PlayConfig);
        _phase = AppPhase.Playing;
        SynchronizeUiState();
        SetStatus($"▶ Playing — ×{_state.PlayConfig.SpeedMultiplier:0.##}");
    }

    private void StopPlayback() => _playback?.Stop();

    private void OnPlaybackStopped()
    {
        BeginInvokeIfAlive(OnPlaybackFinished);
    }

    private void OnPlaybackFinished()
    {
        _playback?.Dispose();
        _playback = null;
        _phase = AppPhase.Idle;
        SynchronizeUiState();
        SetStatus(_state.HasEvents
            ? $"Ready — {_state.Events.Count:N0} events loaded"
            : "Idle");
    }

    // ── File I/O ──────────────────────────────────────────────────────────────

    private void LoadFile(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
            _state.Events   = MacroSerializer.Deserialize(fs);
            _state.FilePath = path;
            SynchronizeUiState();
            SetStatus($"Loaded {_state.Events.Count:N0} events — {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to open file:\n{ex.Message}",
                "Open Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveToFile(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
            MacroSerializer.Serialize(fs, _state.Events);
            _state.FilePath = path;
            SetStatus($"Saved {_state.Events.Count:N0} events — {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to save file:\n{ex.Message}",
                "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveAs()
    {
        TopMost = false;
        try
        {
            using var dlg = new SaveFileDialog
            {
                Title      = "Save Macro Recording",
                Filter     = "Scriptr Macro (*.rec)|*.rec",
                DefaultExt = "rec",
                FileName   = _state.FilePath is not null
                    ? Path.GetFileName(_state.FilePath)
                    : "macro.rec"
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                SaveToFile(dlg.FileName);
        }
        finally { TopMost = true; }
    }

    // ── UI state machine ──────────────────────────────────────────────────────

    private void SynchronizeUiState()
    {
        bool idle      = _phase == AppPhase.Idle;
        bool recording = _phase == AppPhase.Recording;
        bool playing   = _phase == AppPhase.Playing;

        _btnOpen.Enabled     = idle;
        _btnSave.Enabled     = idle && _state.HasEvents;
        _btnCompile.Enabled  = idle;
        _btnSettings.Enabled = idle;

        _btnRecord.Text      = recording ? "⏹ Stop"  : "🔴 Record";
        _btnRecord.ForeColor = recording ? Color.Red : SystemColors.ControlText;
        _btnRecord.Enabled   = idle || recording;

        _btnPlay.Text        = playing ? "⏹ Abort" : "▶ Play";
        _btnPlay.ForeColor   = playing ? Color.DarkGreen : SystemColors.ControlText;
        _btnPlay.Enabled     = (idle && _state.HasEvents) || playing;
    }

    private void SetStatus(string text) => _statusLabel.Text = text;

    private void Timer_Tick(object? sender, EventArgs e)
    {
        RecordingSession? session = _recording;
        if (_phase == AppPhase.Recording && session is not null)
        {
            _blinkState = !_blinkState;
            _btnRecord.ForeColor = _blinkState ? Color.Red : Color.DarkRed;
            SetStatus($"🔴 Recording — {session.EventCount:N0} events");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ToggleVisibility()
    {
        if (Visible) { Hide(); return; }
        Show();
        BringToFront();
        Activate();
    }

    private void StopAllActive()
    {
        if      (_phase == AppPhase.Recording) _ = StopRecordingAsync();
        else if (_phase == AppPhase.Playing)   StopPlayback();
    }

    private void BeginInvokeIfAlive(Action action)
    {
        if (!IsHandleCreated || IsDisposed) return;
        BeginInvoke(action);
    }
}
