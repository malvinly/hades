namespace Hades.SaveFormat;

/// <summary>Reads and writes the luabins serialization format used for the save's Lua state.</summary>
public static class Luabins
{
    private const byte Nil = (byte)'-', False = (byte)'0', True = (byte)'1',
        Number = (byte)'N', String = (byte)'S', Table = (byte)'T';

    public static List<LuaValue> Read(byte[] data)
    {
        using var reader = new BinaryReader(new MemoryStream(data));
        var count = reader.ReadByte();
        var values = new List<LuaValue>(count);
        for (var i = 0; i < count; i++)
            values.Add(ReadValue(reader));
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

    private static LuaValue ReadValue(BinaryReader reader)
    {
        var tag = reader.ReadByte();
        switch (tag)
        {
            case Nil: return LuaNil.Instance;
            case False: return new LuaBool(false);
            case True: return new LuaBool(true);
            case Number: return new LuaNumber(reader.ReadInt64());
            case String: return new LuaString(reader.ReadBytes(reader.ReadInt32()));
            case Table:
                var arraySize = reader.ReadInt32();
                var hashSize = reader.ReadInt32();
                var entries = new List<KeyValuePair<LuaValue, LuaValue>>(arraySize + hashSize);
                for (var i = 0; i < arraySize + hashSize; i++)
                {
                    var key = ReadValue(reader);
                    entries.Add(new(key, ReadValue(reader)));
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
        }
    }
}
