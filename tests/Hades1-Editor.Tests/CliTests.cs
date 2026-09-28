using Hades.SaveFormat;

namespace Hades1Editor.Tests;

public class CliTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("hades1-editor-tests").FullName;
    private string Input => Path.Combine(_dir, "in.sav");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Show_prints_current_values()
    {
        var output = new StringWriter();

        var exitCode = Cli.Run(["show", TestData.Profile1], output, TextWriter.Null);

        Assert.Equal(0, exitCode);
        var lines = output.ToString().Split(Environment.NewLine);
        Assert.Contains(lines, l => l.StartsWith("Darkness held") && l.EndsWith("10888"));
        Assert.Contains(lines, l => l.StartsWith("Chthonic Keys held") && l.EndsWith("6"));
        Assert.Contains(lines, l => l.StartsWith("God Mode level") && l.EndsWith("0"));
        Assert.Contains(lines, l => l.StartsWith("God Mode on (header)"));
        Assert.Contains(lines, l => l.StartsWith("God Mode on (ConfigOptionCache)"));
    }

    [Fact]
    public void In_place_edit_writes_backup_first()
    {
        File.Copy(TestData.Profile1, Input);
        var original = File.ReadAllBytes(Input);

        var exitCode = Cli.Run(["set", Input, "--in-place", "--keys", "50"], TextWriter.Null, TextWriter.Null);

        Assert.Equal(0, exitCode);
        var backup = Directory.GetFiles(_dir, "in.sav.*.bak").Single();
        Assert.Equal(original, File.ReadAllBytes(backup));
        Assert.Equal(50, Hades1Fields.All.Single(f => f.Name == "keys").Get(HadesSave.Read(File.ReadAllBytes(Input))));
    }
}
