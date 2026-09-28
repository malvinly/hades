using Hades.SaveFormat;

namespace HadesEditor.Tests;

public class CliTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("hades-editor-tests").FullName;
    private string Input => Path.Combine(_dir, "in.sav");
    private string Output => Path.Combine(_dir, "out.sav");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Show_prints_hades1_values()
    {
        var lines = Show(TestData.Hades1);

        Assert.Contains("Game                             Hades 1", lines);
        Assert.Contains("Darkness held                    10888", lines);
        Assert.Contains("Chthonic Keys held               6", lines);
        Assert.Contains("God Mode level                   0", lines);
        Assert.Contains("Lifetime Darkness (header)       198", lines);
        Assert.Contains("God Mode on (header)             False", lines);
        Assert.Contains("God Mode on (ConfigOptionCache)  False", lines);
    }

    [Fact]
    public void Show_prints_hades2_values()
    {
        var lines = Show(TestData.Hades2);

        Assert.Contains("Game                             Hades II", lines);
        Assert.Contains("Bones held                       3785", lines);
        Assert.Contains("Ashes held                       760", lines);
        Assert.Contains("Psyche held                      934", lines);
        Assert.Contains("God Mode level                   0", lines);
        Assert.Contains("God Mode on (header)             False", lines);
    }

    [Fact]
    public void Show_reports_a_missing_field_and_still_prints_the_rest()
    {
        var save = HadesSave.Read(File.ReadAllBytes(TestData.Hades1));
        var resources = (LuaTable)save.RootTable.Find(["GameState", "Resources"])!;
        var index = resources.Entries.FindIndex(e => e.Key is LuaString s && s.Equals("LockKeys"));
        resources.Entries[index] = new(new LuaString("LockKeyx"u8.ToArray()), resources.Entries[index].Value);
        File.WriteAllBytes(Input, save.Write());

        var lines = Show(Input);

        Assert.Contains("Darkness held                    10888", lines);
        Assert.Contains("Chthonic Keys held               missing (not yet in this save)", lines);
        Assert.Contains("God Mode level                   0", lines);
    }

    [Fact]
    public void Set_writes_the_output_and_leaves_the_input_untouched()
    {
        File.Copy(TestData.Hades1, Input);
        var original = File.ReadAllBytes(Input);
        var output = new StringWriter();

        var exitCode = Cli.Run(["set", Input, "--out", Output, "--keys", "50", "--godmode-level", "30"], output, TextWriter.Null);

        Assert.Equal(0, exitCode);
        Assert.Equal(original, File.ReadAllBytes(Input));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
        var written = HadesSave.Read(File.ReadAllBytes(Output));
        var fields = GameFields.ByGame[Game.Hades1];
        Assert.Equal(50, fields.Single(f => f.Name == "keys").Get(written));
        Assert.Equal(30, fields.Single(f => f.Name == "godmode-level").Get(written));
        Assert.Equal(10888, fields.Single(f => f.Name == "darkness").Get(written));
        Assert.Contains($"Saved to {Output}", output.ToString());
    }

    private static string[] Show(string path)
    {
        var output = new StringWriter();
        var exitCode = Cli.Run(["show", path], output, TextWriter.Null);
        Assert.Equal(0, exitCode);
        return output.ToString().Split(Environment.NewLine);
    }
}
