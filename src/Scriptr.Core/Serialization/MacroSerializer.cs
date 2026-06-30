using System.Buffers;
using Scriptr.Core.Events;

namespace Scriptr.Core.Serialization;

// .rec binary layout (all integers little-endian / IEEE 754 floats):
//
//   Offset  Size  Field
//   ──────  ────  ────────────────────────────────────────────────
//      0      4   Magic       0x53 0x43 0x52 0x49  ('SCRI')
//      4      2   Version     uint16 = 1
//      6      2   Reserved    uint16 = 0
//      8      4   EventCount  uint32
//
// Per-event field layout (27 bytes, mirrors InputEvent declaration order):
//
//   Off  Size  Field
//   ───  ────  ──────────────
//    0     8   TimestampUs   int64
//    8     1   EventType     byte
//    9     4   NormalizedX   float32
//   13     4   NormalizedY   float32
//   17     1   Button        byte
//   18     4   ScrollDelta   int32
//   22     2   VirtualKey    uint16
//   24     2   ScanCode      uint16
//   26     1   Flags         byte
//             ─────
//              27 bytes total

public static class MacroSerializer
{
    public static ReadOnlySpan<byte> FileMagic => "SCRI"u8;
    public const ushort FormatVersion  = 1;
    public const int    EventByteSize  = 27;
    public const int    HeaderByteSize = 12;   // 4+2+2+4

    // ── Serialization ─────────────────────────────────────────────────────────

    public static void Serialize(Stream stream, IReadOnlyList<InputEvent> events)
    {
        // Rent one contiguous buffer; cap batch size to avoid LOH pressure.
        int eventsPerBatch = Math.Max(1, (256 * 1024) / EventByteSize);   // ~9709 events @ 256 KB
        byte[] buf = ArrayPool<byte>.Shared.Rent(
            HeaderByteSize + Math.Min(events.Count, eventsPerBatch) * EventByteSize);

        try
        {
            // Header (12 bytes)
            int pos = 0;
            buf[pos++] = (byte)'S';
            buf[pos++] = (byte)'C';
            buf[pos++] = (byte)'R';
            buf[pos++] = (byte)'I';
            WriteU16(buf, ref pos, FormatVersion);
            WriteU16(buf, ref pos, 0);                        // reserved
            WriteU32(buf, ref pos, (uint)events.Count);
            stream.Write(buf, 0, pos);

            // Events in batches
            int written = 0;
            while (written < events.Count)
            {
                int batch = Math.Min(eventsPerBatch, events.Count - written);
                pos = 0;
                for (int i = 0; i < batch; i++)
                {
                    InputEvent evt = events[written + i];
                    WriteEvent(buf, ref pos, in evt);
                }
                stream.Write(buf, 0, pos);
                written += batch;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    private static void WriteEvent(byte[] buf, ref int pos, in InputEvent e)
    {
        WriteI64(buf,  ref pos, e.TimestampUs);
        buf[pos++] = (byte)e.EventType;
        WriteF32(buf,  ref pos, e.NormalizedX);
        WriteF32(buf,  ref pos, e.NormalizedY);
        buf[pos++] = (byte)e.Button;
        WriteI32(buf,  ref pos, e.ScrollDelta);
        WriteU16(buf,  ref pos, e.VirtualKey);
        WriteU16(buf,  ref pos, e.ScanCode);
        buf[pos++] = (byte)e.Flags;
    }

    // ── Deserialization ───────────────────────────────────────────────────────

    public static List<InputEvent> Deserialize(Stream stream)
    {
        Span<byte> hdr = stackalloc byte[HeaderByteSize];
        stream.ReadExactly(hdr);

        if (!hdr[..4].SequenceEqual(FileMagic))
            throw new InvalidDataException("Not a valid .rec file: incorrect magic bytes.");

        ushort version = RdU16(hdr, 4);
        if (version != FormatVersion)
            throw new InvalidDataException(
                $"Unsupported .rec format version {version}; expected {FormatVersion}.");

        // hdr[6..7] = reserved, silently ignored for forward-compat
        uint count = RdU32(hdr, 8);

        if (count > 10_000_000u)
            throw new InvalidDataException(
                $"Event count {count:N0} exceeds sanity limit of 10 M — file may be corrupt.");

        var events = new List<InputEvent>((int)count);
        if (count == 0) return events;

        int eventsPerBatch = Math.Max(1, (256 * 1024) / EventByteSize);
        byte[] buf = ArrayPool<byte>.Shared.Rent(eventsPerBatch * EventByteSize);

        try
        {
            int remaining = (int)count;
            while (remaining > 0)
            {
                int batch     = Math.Min(eventsPerBatch, remaining);
                int readBytes = batch * EventByteSize;
                stream.ReadExactly(buf, 0, readBytes);

                int pos = 0;
                for (int i = 0; i < batch; i++)
                    events.Add(ReadEvent(buf, ref pos));

                remaining -= batch;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }

        return events;
    }

    private static InputEvent ReadEvent(byte[] buf, ref int pos)
    {
        long           timestampUs = RdI64(buf, ref pos);
        InputEventType eventType   = (InputEventType)buf[pos++];
        float          normX       = RdF32(buf, ref pos);
        float          normY       = RdF32(buf, ref pos);
        MouseButton    button      = (MouseButton)buf[pos++];
        int            scrollDelta = RdI32(buf, ref pos);
        ushort         virtualKey  = RdU16(buf, ref pos);
        ushort         scanCode    = RdU16(buf, ref pos);
        KeyFlags       flags       = (KeyFlags)buf[pos++];

        return new InputEvent(eventType, timestampUs, normX, normY, button, scrollDelta, virtualKey, scanCode, flags);
    }

    // ── Explicit little-endian primitives ────────────────────────────────────
    // Manual encoding guarantees correct layout on both LE and BE hosts and
    // avoids BinaryWriter's per-field stream-write overhead.

    private static void WriteU16(byte[] b, ref int p, ushort v)
    { b[p++] = (byte)v; b[p++] = (byte)(v >> 8); }

    private static void WriteU32(byte[] b, ref int p, uint v)
    { b[p++] = (byte)v; b[p++] = (byte)(v >> 8); b[p++] = (byte)(v >> 16); b[p++] = (byte)(v >> 24); }

    private static void WriteI32(byte[] b, ref int p, int v)
    => WriteU32(b, ref p, (uint)v);

    private static void WriteI64(byte[] b, ref int p, long v)
    {
        b[p++] = (byte)v;         b[p++] = (byte)(v >> 8);
        b[p++] = (byte)(v >> 16); b[p++] = (byte)(v >> 24);
        b[p++] = (byte)(v >> 32); b[p++] = (byte)(v >> 40);
        b[p++] = (byte)(v >> 48); b[p++] = (byte)(v >> 56);
    }

    private static void WriteF32(byte[] b, ref int p, float v)
    => WriteU32(b, ref p, BitConverter.SingleToUInt32Bits(v));

    // ── Sequential read helpers (advance pos) ────────────────────────────────

    private static ushort RdU16(byte[] b, ref int p)
    { ushort v = (ushort)(b[p] | (b[p + 1] << 8)); p += 2; return v; }

    private static int RdI32(byte[] b, ref int p)
    { int v = b[p] | (b[p+1] << 8) | (b[p+2] << 16) | (b[p+3] << 24); p += 4; return v; }

    private static long RdI64(byte[] b, ref int p)
    {
        long v = (long)b[p]
               | ((long)b[p+1] << 8)  | ((long)b[p+2] << 16) | ((long)b[p+3] << 24)
               | ((long)b[p+4] << 32) | ((long)b[p+5] << 40) | ((long)b[p+6] << 48)
               | ((long)b[p+7] << 56);
        p += 8;
        return v;
    }

    private static float RdF32(byte[] b, ref int p)
    {
        uint bits = (uint)(b[p] | (b[p+1] << 8) | (b[p+2] << 16) | (b[p+3] << 24));
        p += 4;
        return BitConverter.UInt32BitsToSingle(bits);
    }

    // ── Fixed-position read helpers (no pos advance — used for header) ───────

    private static ushort RdU16(ReadOnlySpan<byte> b, int p) =>
        (ushort)(b[p] | (b[p + 1] << 8));

    private static uint RdU32(ReadOnlySpan<byte> b, int p) =>
        (uint)(b[p] | (b[p+1] << 8) | (b[p+2] << 16) | (b[p+3] << 24));
}
