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

        AssertFails("Path not found or not a number: GameState.EasyModeLevel", "set", Input, "--out", Output, "--godmode-level", "5");
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

    [Fact]
    public void Unsupported_version_fails_and_writes_nothing()
    {
        var bytes = File.ReadAllBytes(TestData.Hades1);
        bytes[8] = 17;
        BitConverter.TryWriteBytes(bytes.AsSpan(4), Adler32(bytes.AsSpan(8)));
        File.WriteAllBytes(Input, bytes);

        AssertFails("Save version 17 is not supported", "set", Input, "--out", Output, "--darkness", "1");
    }

    [Theory]
    [InlineData("--godmode-level", "31")]
    [InlineData("--godmode-level", "-1")]
    [InlineData("--darkness", "-5")]
    public void Out_of_range_value_fails_and_writes_nothing(string option, string value)
    {
        File.Copy(TestData.Hades1, Input);

        AssertFails($"{option[2..]} must be between", "set", Input, "--out", Output, option, value);
    }

    [Fact]
    public void Non_integer_value_fails_and_writes_nothing()
    {
        File.Copy(TestData.Hades1, Input);

        AssertFails("--keys needs a whole number", "set", Input, "--out", Output, "--keys", "1.5");
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

    [Theory]
    [InlineData("in.sav")]
    [InlineData("IN.SAV")]
    public void Output_equal_to_input_is_refused(string outputName)
    {
        File.Copy(TestData.Hades1, Input);

        AssertFails("Output path must be a different file", "set", Input, "--out", Path.Combine(_dir, outputName), "--darkness", "1");
        Assert.Equal(File.ReadAllBytes(TestData.Hades1), File.ReadAllBytes(Input));
    }

    [Fact]
    public void Missing_out_option_fails()
    {
        File.Copy(TestData.Hades1, Input);

        AssertFails("--out <path> is required", "set", Input, "--darkness", "1");
    }

    [Fact]
    public void Repeated_option_fails_and_writes_nothing()
    {
        File.Copy(TestData.Hades1, Input);

        AssertFails("--keys was given more than once", "set", Input, "--out", Output, "--keys", "5", "--keys", "50");
    }

    [Fact]
    public void Option_without_value_fails_and_writes_nothing()
    {
        File.Copy(TestData.Hades1, Input);

        AssertFails("Expected --option value", "set", Input, "--out", Output, "--keys");
    }

    [Fact]
    public void Failed_move_into_place_leaves_no_files_behind()
    {
        File.Copy(TestData.Hades1, Input);
        Directory.CreateDirectory(Output); // a directory where the output file should go makes the final move fail

        var error = new StringWriter();
        var exitCode = Cli.Run(["set", Input, "--out", Output, "--keys", "5"], TextWriter.Null, error);

        Assert.NotEqual(0, exitCode);
        Assert.StartsWith("Error:", error.ToString());
        Assert.Equal([Input], Directory.GetFiles(_dir));
    }

    private void AssertFails(string expectedMessagePart, params string[] args)
    {
        var error = new StringWriter();

        var exitCode = Cli.Run(args, TextWriter.Null, error);

        Assert.NotEqual(0, exitCode);
        Assert.Contains(expectedMessagePart, error.ToString());
        Assert.False(File.Exists(Output), "No output file should be written on error");
        Assert.All(Directory.GetFiles(_dir), f => Assert.Equal(Input, f));
    }

    private static uint Adler32(ReadOnlySpan<byte> data)
    {
        uint a = 1, b = 0;
        foreach (var value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }
}
