using Hades.SaveFormat;

namespace HadesEditor.Tests;

public class CliTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("hades-editor-tests").FullName;
    private string Input => Path.Combine(_dir, "in.sav");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Show_prints_hades1_values()
    {
        var lines = Show(TestData.Hades1);

        Assert.Contains(lines, l => l.StartsWith("Game") && l.EndsWith("Hades 1"));
        Assert.Contains(lines, l => l.StartsWith("Darkness held") && l.EndsWith("10888"));
        Assert.Contains(lines, l => l.StartsWith("Chthonic Keys held") && l.EndsWith("6"));
        Assert.Contains(lines, l => l.StartsWith("God Mode level") && l.EndsWith("0"));
        Assert.Contains(lines, l => l.StartsWith("God Mode on (header)"));
        Assert.Contains(lines, l => l.StartsWith("God Mode on (ConfigOptionCache)"));
    }

    [Fact]
    public void Show_prints_hades2_values()
    {
        var lines = Show(TestData.Hades2);

        Assert.Contains(lines, l => l.StartsWith("Game") && l.EndsWith("Hades II"));
        Assert.Contains(lines, l => l.StartsWith("Bones held") && l.EndsWith("3785"));
        Assert.Contains(lines, l => l.StartsWith("Ashes held") && l.EndsWith("760"));
        Assert.Contains(lines, l => l.StartsWith("Psyche held") && l.EndsWith("934"));
        Assert.Contains(lines, l => l.StartsWith("God Mode level") && l.EndsWith("0"));
        Assert.Contains(lines, l => l.StartsWith("God Mode on (header)"));
    }

    [Fact]
    public void In_place_edit_writes_backup_first()
    {
        File.Copy(TestData.Hades1, Input);
        var original = File.ReadAllBytes(Input);

        var exitCode = Cli.Run(["set", Input, "--in-place", "--keys", "50"], TextWriter.Null, TextWriter.Null);

        Assert.Equal(0, exitCode);
        var backup = Directory.GetFiles(_dir, "in.sav.*.bak").Single();
        Assert.Equal(original, File.ReadAllBytes(backup));
        var keys = GameFields.ByGame[Game.Hades1].Single(f => f.Name == "keys");
        Assert.Equal(50, keys.Get(HadesSave.Read(File.ReadAllBytes(Input))));
    }

    private static string[] Show(string path)
    {
        var output = new StringWriter();
        var exitCode = Cli.Run(["show", path], output, TextWriter.Null);
        Assert.Equal(0, exitCode);
        return output.ToString().Split(Environment.NewLine);
    }
}
