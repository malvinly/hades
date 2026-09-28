using Hades.SaveFormat;

namespace HadesEditor.Tests;

public class CliErrorTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("hades-editor-tests").FullName;
    private string Input => Path.Combine(_dir, "in.sav");
    private string Output => Path.Combine(_dir, "out.sav");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Missing_path_fails_and_writes_nothing()
    {
        var save = HadesSave.Read(File.ReadAllBytes(TestData.Hades1));
        var gameState = (LuaTable)save.RootTable["GameState"]!;
        var index = gameState.Entries.FindIndex(e => e.Key is LuaString s && s.Equals("EasyModeLevel"));
        gameState.Entries[index] = new(new LuaString("EasyModeLevex"u8.ToArray()), gameState.Entries[index].Value);
        File.WriteAllBytes(Input, save.Write());

        AssertFails("Path not found", "set", Input, "--out", Output, "--godmode-level", "5");
    }

    [Fact]
    public void Wrong_magic_fails_and_writes_nothing()
    {
        var bytes = File.ReadAllBytes(TestData.Hades1);
        bytes[0] ^= 0xFF;
        File.WriteAllBytes(Input, bytes);

        AssertFails("bad magic", "set", Input, "--out", Output, "--darkness", "1");
    }

    [Fact]
    public void Bad_checksum_fails_and_writes_nothing()
    {
        var bytes = File.ReadAllBytes(TestData.Hades1);
        bytes[100] ^= 0xFF;
        File.WriteAllBytes(Input, bytes);

        AssertFails("Checksum mismatch", "set", Input, "--out", Output, "--darkness", "1");
    }

    [Theory]
    [InlineData("--godmode-level", "31")]
    [InlineData("--godmode-level", "-1")]
    [InlineData("--darkness", "-5")]
    [InlineData("--keys", "1.5")]
    public void Out_of_range_value_fails_and_writes_nothing(string option, string value)
    {
        File.Copy(TestData.Hades1, Input);

        AssertFails(option[2..], "set", Input, "--out", Output, option, value);
    }

    [Fact]
    public void Hades2_field_on_hades1_save_fails_and_writes_nothing()
    {
        File.Copy(TestData.Hades1, Input);

        AssertFails("--bones is not a Hades 1 field", "set", Input, "--out", Output, "--bones", "100");
    }

    [Fact]
    public void Hades1_field_on_hades2_save_fails_and_writes_nothing()
    {
        File.Copy(TestData.Hades2, Input);

        AssertFails("--darkness is not a Hades II field", "set", Input, "--out", Output, "--darkness", "100", "--bones", "100");
    }

    [Fact]
    public void Output_equal_to_input_is_refused()
    {
        File.Copy(TestData.Hades1, Input);

        AssertFails("--in-place", "set", Input, "--out", Input, "--darkness", "1");
    }

    private void AssertFails(string expectedMessagePart, params string[] args)
    {
        var error = new StringWriter();

        var exitCode = Cli.Run(args, TextWriter.Null, error);

        Assert.NotEqual(0, exitCode);
        Assert.Contains(expectedMessagePart, error.ToString());
        Assert.False(File.Exists(Output), "No output file should be written on error");
    }
}
