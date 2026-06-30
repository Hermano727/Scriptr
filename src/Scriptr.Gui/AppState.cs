using Scriptr.Core.Events;
using Scriptr.Core.Playback;

namespace Scriptr.Gui;

internal sealed class AppState
{
    public List<InputEvent>  Events        { get; set; } = [];
    public string?           FilePath      { get; set; }
    public PlaybackConfig    PlayConfig    { get; set; } = PlaybackConfig.Once();
    public HotkeyBinding     RecordHotkey  { get; set; } = new(Keys.F10);
    public HotkeyBinding     AbortHotkey   { get; set; } = new(Keys.F8);

    public bool HasEvents => Events.Count > 0;
}
