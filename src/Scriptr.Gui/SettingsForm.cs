using Scriptr.Core.Playback;

namespace Scriptr.Gui;

internal sealed class SettingsForm : Form
{
    // ── Result properties (read by MainForm after DialogResult.OK) ────────────
    public PlaybackConfig ResultPlayConfig   { get; private set; }
    public HotkeyBinding  ResultRecordHotkey { get; private set; }
    public HotkeyBinding  ResultAbortHotkey  { get; private set; }

    // Pending bindings captured while the form is open
    private HotkeyBinding _pendingRecord;
    private HotkeyBinding _pendingAbort;

    // Controls
    private ComboBox      _cmbSpeed    = null!;
    private ComboBox      _cmbMode     = null!;
    private NumericUpDown _nudRepeat   = null!;
    private Label         _lblRepeat   = null!;
    private TextBox       _txtRecord   = null!;
    private TextBox       _txtAbort    = null!;

    private static readonly float[] SpeedValues = [1f, 2f, 5f, 10f, 100f];

    public SettingsForm(PlaybackConfig config, HotkeyBinding recordKey, HotkeyBinding abortKey)
    {
        ResultPlayConfig   = config;
        ResultRecordHotkey = recordKey;
        ResultAbortHotkey  = abortKey;
        _pendingRecord     = recordKey;
        _pendingAbort      = abortKey;

        InitializeComponent();
        LoadValues(config, recordKey, abortKey);
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text            = "Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        ClientSize      = new Size(340, 278);

        int lx = 16, cx = 130, ry = 14;

        // ── Playback section ─────────────────────────────────────────────────
        var grpPlay = new GroupBox
        {
            Text     = "Playback",
            Location = new Point(lx, ry),
            Size     = new Size(308, 118)
        };

        var lblSpeed = new Label { Text = "Speed:", Location = new Point(12, 26), AutoSize = true };
        _cmbSpeed = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location      = new Point(cx - 14, 22),
            Width         = 120
        };
        _cmbSpeed.Items.AddRange(["1× (Normal)", "2×", "5×", "10×", "100×"]);

        var lblMode = new Label { Text = "Loop mode:", Location = new Point(12, 58), AutoSize = true };
        _cmbMode = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location      = new Point(cx - 14, 54),
            Width         = 120
        };
        _cmbMode.Items.AddRange(["Once", "Repeat N times", "Forever"]);
        _cmbMode.SelectedIndexChanged += CmbMode_SelectedIndexChanged;

        _lblRepeat = new Label { Text = "Repeat count:", Location = new Point(12, 90), AutoSize = true };
        _nudRepeat = new NumericUpDown
        {
            Minimum  = 1,
            Maximum  = 9999,
            Value    = 2,
            Location = new Point(cx - 14, 86),
            Width    = 70
        };

        grpPlay.Controls.AddRange([lblSpeed, _cmbSpeed, lblMode, _cmbMode, _lblRepeat, _nudRepeat]);

        // ── Hotkeys section ───────────────────────────────────────────────────
        var grpHotkeys = new GroupBox
        {
            Text     = "Global Hotkeys  (click box, press key combo)",
            Location = new Point(lx, ry + 126),
            Size     = new Size(308, 88)
        };

        var lblRec = new Label { Text = "Record / Stop:", Location = new Point(12, 28), AutoSize = true };
        _txtRecord = MakeHotkeyBox(new Point(cx - 14, 24), RecordBox_KeyDown);

        var lblAbt = new Label { Text = "Abort Playback:", Location = new Point(12, 58), AutoSize = true };
        _txtAbort = MakeHotkeyBox(new Point(cx - 14, 54), AbortBox_KeyDown);

        grpHotkeys.Controls.AddRange([lblRec, _txtRecord, lblAbt, _txtAbort]);

        // ── Buttons ───────────────────────────────────────────────────────────
        var btnReset = new Button
        {
            Text     = "Reset defaults",
            Location = new Point(lx, 240),
            Width    = 110,
            Height   = 26
        };
        btnReset.Click += BtnReset_Click;

        var btnCancel = new Button
        {
            Text           = "Cancel",
            Location       = new Point(168, 240),
            Width          = 72,
            Height         = 26,
            DialogResult   = DialogResult.Cancel
        };

        var btnOk = new Button
        {
            Text         = "OK",
            Location     = new Point(252, 240),
            Width        = 72,
            Height       = 26,
            DialogResult = DialogResult.OK
        };
        btnOk.Click += BtnOk_Click;

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.AddRange([grpPlay, grpHotkeys, btnReset, btnCancel, btnOk]);

        ResumeLayout(false);
        PerformLayout();
    }

    private static TextBox MakeHotkeyBox(Point location, KeyEventHandler onKeyDown)
    {
        var tb = new TextBox
        {
            ReadOnly          = true,
            Location          = location,
            Width             = 120,
            ShortcutsEnabled  = false,
            BackColor         = SystemColors.Window,
            Cursor            = Cursors.IBeam
        };
        tb.KeyDown += onKeyDown;
        return tb;
    }

    // ── Load current values into controls ─────────────────────────────────────

    private void LoadValues(PlaybackConfig config, HotkeyBinding rec, HotkeyBinding abt)
    {
        // Speed
        int speedIdx = Array.FindIndex(SpeedValues, v => Math.Abs(v - config.SpeedMultiplier) < 0.01f);
        _cmbSpeed.SelectedIndex = speedIdx >= 0 ? speedIdx : 0;

        // Loop mode
        _cmbMode.SelectedIndex = config.Mode switch
        {
            LoopMode.RepeatN    => 1,
            LoopMode.Continuous => 2,
            _                   => 0
        };

        // Repeat count
        if (config.Mode == LoopMode.RepeatN && config.RepeatCount > 0)
            _nudRepeat.Value = Math.Min(config.RepeatCount, 9999);
        UpdateRepeatVisibility();

        // Hotkeys
        _txtRecord.Text = rec.DisplayText;
        _txtAbort.Text  = abt.DisplayText;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void CmbMode_SelectedIndexChanged(object? sender, EventArgs e)
        => UpdateRepeatVisibility();

    private void UpdateRepeatVisibility()
    {
        bool show = _cmbMode.SelectedIndex == 1;  // "Repeat N times"
        _lblRepeat.Visible = show;
        _nudRepeat.Visible = show;
    }

    private void RecordBox_KeyDown(object? sender, KeyEventArgs e)
        => CaptureHotkey(e, ref _pendingRecord, _txtRecord);

    private void AbortBox_KeyDown(object? sender, KeyEventArgs e)
        => CaptureHotkey(e, ref _pendingAbort, _txtAbort);

    private static void CaptureHotkey(KeyEventArgs e, ref HotkeyBinding target, TextBox box)
    {
        e.SuppressKeyPress = true;
        e.Handled          = true;

        // Ignore bare modifier presses — wait for a real key.
        if (e.KeyCode is Keys.ControlKey or Keys.LControlKey or Keys.RControlKey
                      or Keys.ShiftKey   or Keys.LShiftKey   or Keys.RShiftKey
                      or Keys.Menu       or Keys.LMenu        or Keys.RMenu
                      or Keys.LWin       or Keys.RWin) return;

        target   = new HotkeyBinding(e.KeyCode, e.Modifiers & Keys.Modifiers);
        box.Text = target.DisplayText;
    }

    private void BtnReset_Click(object? sender, EventArgs e)
    {
        _pendingRecord  = new HotkeyBinding(Keys.F10);
        _pendingAbort   = new HotkeyBinding(Keys.F8);
        _txtRecord.Text = _pendingRecord.DisplayText;
        _txtAbort.Text  = _pendingAbort.DisplayText;
        _cmbSpeed.SelectedIndex = 0;
        _cmbMode.SelectedIndex  = 0;
        _nudRepeat.Value        = 2;
        UpdateRepeatVisibility();
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        float speed = SpeedValues[Math.Max(0, _cmbSpeed.SelectedIndex)];

        ResultPlayConfig = _cmbMode.SelectedIndex switch
        {
            1 => PlaybackConfig.Repeat((int)_nudRepeat.Value, speed),
            2 => PlaybackConfig.Forever(speed),
            _ => PlaybackConfig.Once(speed)
        };

        ResultRecordHotkey = _pendingRecord;
        ResultAbortHotkey  = _pendingAbort;
    }
}
