namespace Scriptr.Core.Events;

[Flags]
public enum KeyFlags : byte
{
    None     = 0,
    Extended = 1 << 0,
    AltDown  = 1 << 1
}
