namespace Scriptr.Core.Serialization;

// Compiler pipeline — appends a .rec payload to a player stub executable.
//
// Output file layout:
//
//   ┌──────────────────────────────────────┐
//   │  stub.exe bytes (PE image, unmodified)│
//   ├──────────────────────────────────────┤
//   │  .rec file bytes  (N bytes)           │
//   ├──────────────────────────────────────┤
//   │  payload length   (8 bytes, int64 LE) │
//   ├──────────────────────────────────────┤
//   │  tail sentinel    (16 bytes, ASCII)   │
//   └──────────────────────────────────────┘
//
// The Windows PE loader ignores trailing bytes beyond the last section,
// so the appended payload is invisible to the OS but readable at runtime.
// The stub's Main reads backwards from the end of its own file to locate it.

public static class MacroCompiler
{
    // "SCRIPTR_EXE_TAIL" — 16 ASCII bytes written verbatim at the very end.
    // Must remain stable across versions; changing it breaks existing compiled macros.
    private static ReadOnlySpan<byte> TailSentinel => "SCRIPTR_EXE_TAIL"u8;
    private const int SentinelLength = 16;
    private const int LengthFieldSize = 8;

    // ── Compile ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads <paramref name="recPath"/>, appends it to a copy of <paramref name="stubPath"/>,
    /// and writes the result to <paramref name="outputExePath"/>.
    /// </summary>
    public static async Task CompileAsync(
        string recPath,
        string stubPath,
        string outputExePath,
        CancellationToken ct = default)
    {
        if (!File.Exists(recPath))
            throw new FileNotFoundException("Source .rec file not found.", recPath);
        if (!File.Exists(stubPath))
            throw new FileNotFoundException(
                "Player stub not found. Ensure Scriptr.Stub.exe is in the same directory as Scriptr.Cli.exe.",
                stubPath);

        // Validate the .rec header before doing any file I/O on the output.
        await ValidateRecFileAsync(recPath, ct);

        byte[] recBytes = await File.ReadAllBytesAsync(recPath, ct);

        // Copy stub → output (overwrite if exists).
        File.Copy(stubPath, outputExePath, overwrite: true);

        // Append: [rec payload][8-byte payload length LE][16-byte tail sentinel]
        await using var fs = new FileStream(outputExePath, FileMode.Append, FileAccess.Write,
            FileShare.None, bufferSize: 65536, useAsync: true);

        await fs.WriteAsync(recBytes, ct);
        await fs.WriteAsync(BitConverter.GetBytes((long)recBytes.Length), ct);

        byte[] sentinel = TailSentinel.ToArray();
        await fs.WriteAsync(sentinel, ct);
    }

    // ── Probe (used by Scriptr.Stub at startup) ───────────────────────────────

    /// <summary>
    /// Reads backwards from the end of <paramref name="exePath"/> to locate an
    /// embedded .rec payload.  Returns <see langword="false"/> if none is present.
    /// </summary>
    public static bool TryReadEmbeddedPayload(string exePath, out byte[] recBytes)
    {
        recBytes = [];

        using var fs = new FileStream(exePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, bufferSize: 4096);

        long fileLen = fs.Length;

        // Minimum viable compiled exe = stub + header + length field + sentinel
        long minLen = MacroSerializer.HeaderByteSize + LengthFieldSize + SentinelLength;
        if (fileLen < minLen) return false;

        // Read and verify tail sentinel.
        Span<byte> tail = stackalloc byte[SentinelLength];
        fs.Seek(-SentinelLength, SeekOrigin.End);
        fs.ReadExactly(tail);
        if (!tail.SequenceEqual(TailSentinel)) return false;

        // Read 8-byte payload length immediately before the sentinel.
        Span<byte> lenBuf = stackalloc byte[LengthFieldSize];
        fs.Seek(-(SentinelLength + LengthFieldSize), SeekOrigin.End);
        fs.ReadExactly(lenBuf);
        long payloadLen = BitConverter.ToInt64(lenBuf);

        // Sanity-check: length must be positive and fit inside the file.
        long payloadOffset = fileLen - SentinelLength - LengthFieldSize - payloadLen;
        if (payloadLen < MacroSerializer.HeaderByteSize || payloadOffset < 0) return false;

        // Read the .rec payload.
        byte[] payload = new byte[payloadLen];
        fs.Seek(payloadOffset, SeekOrigin.Begin);
        fs.ReadExactly(payload);

        recBytes = payload;
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task ValidateRecFileAsync(string path, CancellationToken ct)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize: 64, useAsync: true);

        if (fs.Length < MacroSerializer.HeaderByteSize)
            throw new InvalidDataException($"'{path}' is too small to be a valid .rec file.");

        byte[] hdr = new byte[4];
        await fs.ReadExactlyAsync(hdr, ct);

        if (!hdr.AsSpan().SequenceEqual(MacroSerializer.FileMagic))
            throw new InvalidDataException($"'{path}' does not contain a valid .rec magic header.");
    }
}
