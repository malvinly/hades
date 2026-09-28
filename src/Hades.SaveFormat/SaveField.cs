namespace Hades.SaveFormat;

/// <summary>An editable whole-number value at a fixed path in the save's root table.</summary>
public sealed record SaveField(string Name, string Description, string[] Path, long Min = 0, long Max = 1L << 53)
{
    public double Get(HadesSave save) =>
        save.RootTable.Find(Path) is LuaNumber n ? n.Value
            : throw new SaveFormatException($"Path not found or not a number: {string.Join(".", Path)}");

    public void Set(HadesSave save, long value)
    {
        if (value < Min || value > Max)
            throw new ArgumentException($"{Name} must be between {Min} and {Max}, got {value}");
        save.RootTable.Replace(Path, LuaNumber.From(value));
    }
}
