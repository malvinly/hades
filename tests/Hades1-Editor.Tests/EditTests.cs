using Hades.SaveFormat;

namespace Hades1Editor.Tests;

public class EditTests
{
    [Fact]
    public void Setting_all_fields_changes_exactly_those_leaves()
    {
        var original = HadesSave.Read(File.ReadAllBytes(TestData.Profile1));
        var edited = HadesSave.Read(File.ReadAllBytes(TestData.Profile1));
        var darkness = Hades1Fields.All.Single(f => f.Name == "darkness");
        var keys = Hades1Fields.All.Single(f => f.Name == "keys");
        var godMode = Hades1Fields.All.Single(f => f.Name == "godmode-level");

        darkness.Set(edited, 99999);
        keys.Set(edited, 42);
        godMode.Set(edited, 7);
        var reread = HadesSave.Read(edited.Write());

        Assert.Equal(99999, darkness.Get(reread));
        Assert.Equal(42, keys.Get(reread));
        Assert.Equal(7, godMode.Get(reread));
        var differences = new List<string>();
        Diff(original.Root, reread.Root, "", differences);
        Assert.Equal(
            ["/0/GameState/EasyModeLevel", "/0/GameState/Resources/LockKeys", "/0/GameState/Resources/MetaPoints"],
            differences.Order());
    }

    private static void Diff(IReadOnlyList<LuaValue> a, IReadOnlyList<LuaValue> b, string path, List<string> differences)
    {
        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
            Diff(a[i], b[i], $"{path}/{i}", differences);
    }

    private static void Diff(LuaValue a, LuaValue b, string path, List<string> differences)
    {
        switch (a, b)
        {
            case (LuaTable ta, LuaTable tb):
                Assert.Equal((ta.ArraySize, ta.HashSize, ta.Entries.Count), (tb.ArraySize, tb.HashSize, tb.Entries.Count));
                for (var i = 0; i < ta.Entries.Count; i++)
                {
                    Assert.True(Same(ta.Entries[i].Key, tb.Entries[i].Key), $"Key mismatch at {path}[{i}]");
                    Diff(ta.Entries[i].Value, tb.Entries[i].Value, $"{path}/{ta.Entries[i].Key}", differences);
                }
                break;
            default:
                if (!Same(a, b))
                    differences.Add(path);
                break;
        }
    }

    private static bool Same(LuaValue a, LuaValue b) => (a, b) switch
    {
        (LuaString sa, LuaString sb) => sa.Bytes.AsSpan().SequenceEqual(sb.Bytes),
        _ => a.Equals(b),
    };
}
