namespace Hades.SaveFormat;

/// <summary>Reads and writes the luabins serialization format used for the save's Lua state.</summary>
public static class Luabins
{
    private const byte Nil = (byte)'-', False = (byte)'0', True = (byte)'1',
        Number = (byte)'N', String = (byte)'S', Table = (byte)'T';

    // Lua itself limits nesting to about 200 C calls; real saves nest far less. Guards against stack overflow.
    private const int MaxDepth = 200;

    public static List<LuaValue> Read(byte[] data)
    {
        using var reader = new BinaryReader(new MemoryStream(data));
        var count = reader.ReadByte();
        var values = new List<LuaValue>(count);
        for (var i = 0; i < count; i++)
            values.Add(ReadValue(reader, 0));
        if (reader.BaseStream.Position != data.Length)
            throw new SaveFormatException("Trailing bytes after Lua data");
        return values;
    }

    public static byte[] Write(IReadOnlyList<LuaValue> values)
    {
        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)values.Count);
        foreach (var value in values)
            WriteValue(writer, value);
        writer.Flush();
        return stream.ToArray();
    }

    private static LuaValue ReadValue(BinaryReader reader, int depth)
    {
        if (depth > MaxDepth)
            throw new SaveFormatException($"Lua data nested deeper than {MaxDepth} levels");
        var tag = reader.ReadByte();
        switch (tag)
        {
            case Nil: return LuaNil.Instance;
            case False: return new LuaBool(false);
            case True: return new LuaBool(true);
            case Number: return new LuaNumber(reader.ReadInt64());
            case String: return new LuaString(reader.ReadBytesExact(reader.ReadInt32()));
            case Table:
                var arraySize = reader.ReadInt32();
                var hashSize = reader.ReadInt32();
                var count = (long)arraySize + hashSize;
                // Each entry needs at least two bytes (a one-byte key and a one-byte value).
                if (arraySize < 0 || hashSize < 0 || count > reader.Remaining() / 2)
                    throw new SaveFormatException($"Lua table size {arraySize}+{hashSize} exceeds the remaining data");
                var entries = new List<KeyValuePair<LuaValue, LuaValue>>((int)count);
                for (var i = 0; i < count; i++)
                {
                    var key = ReadValue(reader, depth + 1);
                    entries.Add(new(key, ReadValue(reader, depth + 1)));
                }
                return new LuaTable(arraySize, hashSize, entries);
            default:
                throw new SaveFormatException($"Unknown Lua value tag 0x{tag:X2} at offset {reader.BaseStream.Position - 1}");
        }
    }

    private static void WriteValue(BinaryWriter writer, LuaValue value)
    {
        switch (value)
        {
            case LuaNil: writer.Write(Nil); break;
            case LuaBool b: writer.Write(b.Value ? True : False); break;
            case LuaNumber n: writer.Write(Number); writer.Write(n.Bits); break;
            case LuaString s: writer.Write(String); writer.Write(s.Bytes.Length); writer.Write(s.Bytes); break;
            case LuaTable t:
                writer.Write(Table);
                writer.Write(t.ArraySize);
                writer.Write(t.HashSize);
                foreach (var (key, entryValue) in t.Entries)
                {
                    WriteValue(writer, key);
                    WriteValue(writer, entryValue);
                }
                break;
            default:
                throw new SaveFormatException($"Cannot write Lua value of type {value.GetType().Name}");
        }
    }
}

internal static class BinaryReaderExtensions
{
    public static long Remaining(this BinaryReader reader) => reader.BaseStream.Length - reader.BaseStream.Position;

    /// <summary>Reads exactly count bytes, rejecting lengths that exceed the data instead of allocating or short-reading.</summary>
    public static byte[] ReadBytesExact(this BinaryReader reader, int count)
    {
        if (count < 0 || count > reader.Remaining())
            throw new SaveFormatException($"Length {count} exceeds the remaining data at offset {reader.BaseStream.Position}");
        return reader.ReadBytes(count);
    }
}
