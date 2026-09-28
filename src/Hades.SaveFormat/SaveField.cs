namespace Hades.SaveFormat;

/// <summary>An editable whole-number value at a fixed path in the save's root table.</summary>
/// <remarks>Max defaults to 2^53, the largest range where every whole number is exact in a double.</remarks>
public sealed record SaveField(string Name, string Description, string[] Path, long Min = 0, long Max = 1L << 53)
{
    /// <summary>The current value, or null when the save has no number at this path (young saves may lack a resource).</summary>
    public double? Find(HadesSave save) => save.RootTable.Find(Path) is LuaNumber n ? n.Value : null;

    public double Get(HadesSave save) =>
        Find(save) ?? throw new SaveFormatException($"Path not found or not a number: {string.Join(".", Path)}");

    /// <summary>Replaces the existing number at this path. Refuses if the path is missing or holds a non-number.</summary>
    public void Set(HadesSave save, long value)
    {
        if (value < Min || value > Max)
            throw new ArgumentException($"{Name} must be between {Min} and {Max}, got {value}");
        Get(save);
        save.RootTable.Replace(Path, LuaNumber.From(value));
    }
}
