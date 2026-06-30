using System.Runtime.InteropServices;

namespace Scriptr.Core.Events;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct InputEvent
{
    public readonly long           TimestampUs;
    public readonly InputEventType EventType;
    public readonly float          NormalizedX;
    public readonly float          NormalizedY;
    public readonly MouseButton    Button;
    public readonly int            ScrollDelta;
    public readonly ushort         VirtualKey;
    public readonly ushort         ScanCode;
    public readonly KeyFlags       Flags;

    public InputEvent(
        InputEventType eventType,
        long           timestampUs,
        float          normalizedX,
        float          normalizedY,
        MouseButton    button,
        int            scrollDelta,
        ushort         virtualKey,
        ushort         scanCode,
        KeyFlags       flags)
    {
        EventType   = eventType;
        TimestampUs = timestampUs;
        NormalizedX = normalizedX;
        NormalizedY = normalizedY;
        Button      = button;
        ScrollDelta = scrollDelta;
        VirtualKey  = virtualKey;
        ScanCode    = scanCode;
        Flags       = flags;
    }

    public bool IsMouseEvent =>
        EventType is InputEventType.MouseMove
                  or InputEventType.MouseDown
                  or InputEventType.MouseUp
                  or InputEventType.MouseWheel;

    public bool IsKeyboardEvent =>
        EventType is InputEventType.KeyDown
                  or InputEventType.KeyUp
                  or InputEventType.SysKeyDown
                  or InputEventType.SysKeyUp;
}
