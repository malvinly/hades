using K4os.Compression.LZ4;

namespace Hades.SaveFormat;

public enum Game { Hades1, Hades2 }

/// <summary>A Hades save file: header fields kept verbatim, plus the decoded Lua root values.</summary>
public sealed class HadesSave
{
    private const uint Magic = 0x31424753; // "SGB1"

    // Only versions whose header layout is known are accepted. Adding a version means updating
    // the gate in Read, the Game mapping, and the Grasp/Prestige reads and writes together.
    private const ushort Hades1Version = 16, Hades2Version = 18;

    public required ushort Version { get; init; }
    public required ushort Flags { get; init; }
    public required ulong Timestamp { get; init; }
    public required string Location { get; init; }
    public required uint CompletedRuns { get; init; }
    public required uint AccumulatedMetaPoints { get; init; }
    public required uint ActiveShrinePoints { get; init; }
    /// <summary>Hades II only (version 18): values the game mirrors into the header. Null for Hades 1.</summary>
    public uint? Grasp { get; init; }
    public uint? Prestige { get; init; }
    public required byte EasyMode { get; init; }
    public required byte HardMode { get; init; }
    public required List<string> NotableLuaData { get; init; }
    public required string MapName { get; init; }
    public required string MapName2 { get; init; }
    public required List<LuaValue> Root { get; init; }

    public Game Game => Version == Hades1Version ? Game.Hades1 : Game.Hades2;

    /// <summary>The first Lua root value, which holds the game state tables.</summary>
    public LuaTable RootTable => Root.Count > 0 && Root[0] is LuaTable t
        ? t : throw new SaveFormatException("First Lua root value is not a table");

    public static HadesSave Read(byte[] bytes)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes));
        if (bytes.Length < 8 || reader.ReadUInt32() != Magic)
            throw new SaveFormatException("Not a Hades save file (bad magic)");
        if (reader.ReadUInt32() != Adler32.Compute(bytes.AsSpan(8)))
            throw new SaveFormatException("Checksum mismatch; file is corrupt or truncated");
        var version = reader.ReadUInt16();
        if (version is not (Hades1Version or Hades2Version))
            throw new SaveFormatException($"Save version {version} is not supported (Hades 1 is {Hades1Version}, Hades II is {Hades2Version})");

        var save = new HadesSave
        {
            Version = version,
            Flags = reader.ReadUInt16(),
            Timestamp = reader.ReadUInt64(),
            Location = reader.ReadLengthPrefixedString(),
            CompletedRuns = reader.ReadUInt32(),
            AccumulatedMetaPoints = reader.ReadUInt32(),
            ActiveShrinePoints = reader.ReadUInt32(),
            Grasp = version == Hades2Version ? reader.ReadUInt32() : null,
            Prestige = version == Hades2Version ? reader.ReadUInt32() : null,
            EasyMode = reader.ReadByte(),
            HardMode = reader.ReadByte(),
            NotableLuaData = Enumerable.Range(0, reader.ReadInt32()).Select(_ => reader.ReadLengthPrefixedString()).ToList(),
            MapName = reader.ReadLengthPrefixedString(),
            MapName2 = reader.ReadLengthPrefixedString(),
            Root = Luabins.Read(Lz4Block.Decompress(reader.ReadBytesExact(reader.ReadInt32()))),
        };
        if (reader.BaseStream.Position != bytes.Length)
            throw new SaveFormatException("Trailing bytes after compressed Lua data");
        return save;
    }

    public byte[] Write()
    {
        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Magic);
        writer.Write(0u); // checksum, filled in below
        writer.Write(Version);
        writer.Write(Flags);
        writer.Write(Timestamp);
        writer.WriteLengthPrefixedString(Location);
        writer.Write(CompletedRuns);
        writer.Write(AccumulatedMetaPoints);
        writer.Write(ActiveShrinePoints);
        if (Version == Hades2Version)
        {
            writer.Write(Grasp ?? throw new SaveFormatException("Hades II save is missing Grasp"));
            writer.Write(Prestige ?? throw new SaveFormatException("Hades II save is missing Prestige"));
        }
        writer.Write(EasyMode);
        writer.Write(HardMode);
        writer.Write(NotableLuaData.Count);
        foreach (var item in NotableLuaData)
            writer.WriteLengthPrefixedString(item);
        writer.WriteLengthPrefixedString(MapName);
        writer.WriteLengthPrefixedString(MapName2);
        var compressed = Lz4Block.Compress(Luabins.Write(Root));
        writer.Write(compressed.Length);
        writer.Write(compressed);
        writer.Flush();

        var bytes = stream.ToArray();
        BitConverter.TryWriteBytes(bytes.AsSpan(4), Adler32.Compute(bytes.AsSpan(8)));
        return bytes;
    }
}

internal static class BinaryExtensions
{
    public static string ReadLengthPrefixedString(this BinaryReader reader) =>
        Encoding.UTF8.GetString(reader.ReadBytesExact(reader.ReadInt32()));

    public static void WriteLengthPrefixedString(this BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}

internal static class Adler32
{
    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint a = 1, b = 0;
        foreach (var value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }
}

/// <summary>
/// Raw LZ4 block, no frame. The game compresses with lz4's LZ4_compress_default; K4os at
/// L00_FAST reproduces its output byte for byte, which the round-trip tests depend on.
/// Changing the level or the package version can silently break that.
/// </summary>
internal static class Lz4Block
{
    public static byte[] Decompress(byte[] compressed)
    {
        // Decoded size is not stored; the game's own buffer is ~3 MB, so start at 4 MB and grow.
        for (var capacity = 4 << 20; capacity <= 256 << 20; capacity *= 2)
        {
            var buffer = new byte[capacity];
            var length = LZ4Codec.Decode(compressed, 0, compressed.Length, buffer, 0, buffer.Length);
            if (length >= 0)
                return buffer[..length];
        }
        throw new SaveFormatException("LZ4 block failed to decompress");
    }

    public static byte[] Compress(byte[] data)
    {
        var buffer = new byte[LZ4Codec.MaximumOutputSize(data.Length)];
        var length = LZ4Codec.Encode(data, 0, data.Length, buffer, 0, buffer.Length, LZ4Level.L00_FAST);
        return buffer[..length];
    }
}
