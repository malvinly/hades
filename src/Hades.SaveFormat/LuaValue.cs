namespace Hades.SaveFormat;

/// <summary>A value in the luabins tree. Numbers keep their exact 8 bytes; tables keep entry order and sizes.</summary>
public abstract record LuaValue;

public sealed record LuaNil : LuaValue
{
    public static readonly LuaNil Instance = new();
}

public sealed record LuaBool(bool Value) : LuaValue;

public sealed record LuaNumber(long Bits) : LuaValue
{
    public double Value => BitConverter.Int64BitsToDouble(Bits);
    public static LuaNumber From(double value) => new(BitConverter.DoubleToInt64Bits(value));
}

public sealed record LuaString(byte[] Bytes) : LuaValue
{
    public bool Equals(string other) => Bytes.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(other));
    public override string ToString() => Encoding.UTF8.GetString(Bytes);
}

public sealed record LuaTable(int ArraySize, int HashSize, List<KeyValuePair<LuaValue, LuaValue>> Entries) : LuaValue
{
    public LuaValue? this[string key] => Entries.FirstOrDefault(e => e.Key is LuaString s && s.Equals(key)).Value;

    /// <summary>Follows a path of string keys. Returns null if any step is missing or not a table.</summary>
    public LuaValue? Find(IReadOnlyList<string> path)
    {
        LuaValue? current = this;
        foreach (var key in path)
            current = (current as LuaTable)?[key];
        return current;
    }

    /// <summary>Replaces the value at an existing path. Never adds keys.</summary>
    public void Replace(IReadOnlyList<string> path, LuaValue value)
    {
        var parent = Find(path.Take(path.Count - 1).ToList()) as LuaTable;
        var index = parent?.Entries.FindIndex(e => e.Key is LuaString s && s.Equals(path[^1])) ?? -1;
        if (parent is null || index < 0)
            throw new SaveFormatException($"Path not found: {string.Join(".", path)}");
        parent.Entries[index] = new(parent.Entries[index].Key, value);
    }
}
