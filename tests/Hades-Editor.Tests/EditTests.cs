using Hades.SaveFormat;

namespace HadesEditor.Tests;

public class EditTests
{
    [Fact]
    public void Hades1_edit_changes_exactly_the_requested_leaves() =>
        AssertEditChangesOnly(TestData.Hades1, Game.Hades1,
            new() { ["darkness"] = 99999, ["keys"] = 42, ["godmode-level"] = 7 },
            ["/0/GameState/EasyModeLevel", "/0/GameState/Resources/LockKeys", "/0/GameState/Resources/MetaPoints"]);

    [Fact]
    public void Hades2_edit_changes_exactly_the_requested_leaves() =>
        AssertEditChangesOnly(TestData.Hades2, Game.Hades2,
            new() { ["bones"] = 5000, ["ashes"] = 1000, ["psyche"] = 250, ["godmode-level"] = 3 },
            ["/0/GameState/EasyModeLevel", "/0/GameState/Resources/MemPointsCommon", "/0/GameState/Resources/MetaCardPointsCommon", "/0/GameState/Resources/MetaCurrency"]);

    private static void AssertEditChangesOnly(string path, Game game, Dictionary<string, long> edits, string[] expectedChangedPaths)
    {
        var original = HadesSave.Read(File.ReadAllBytes(path));
        var edited = HadesSave.Read(File.ReadAllBytes(path));
        var fields = GameFields.ByGame[game];
        Assert.Equal(game, edited.Game);

        foreach (var (name, value) in edits)
            fields.Single(f => f.Name == name).Set(edited, value);
        var reread = HadesSave.Read(edited.Write());

        foreach (var (name, value) in edits)
            Assert.Equal(value, fields.Single(f => f.Name == name).Get(reread));
        var differences = new List<string>();
        Diff(original.Root, reread.Root, "", differences);
        Assert.Equal(expectedChangedPaths, differences.Order());
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
