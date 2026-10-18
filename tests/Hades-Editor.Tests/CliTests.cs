using Hades.SaveFormat;

namespace HadesEditor.Tests;

public class CliTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("hades-editor-tests").FullName;
    private string Input => Path.Combine(_dir, "in.sav");
    private string Output => Path.Combine(_dir, "out.sav");
    private string BackupDir => Path.Combine(_dir, "hades-backup");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Show_prints_hades1_values()
    {
        var lines = Show(TestData.Hades1);

        Assert.Contains("Game                             Hades 1", lines);
        Assert.Contains("Darkness held                    10888", lines);
        Assert.Contains("Chthonic Keys held               6", lines);
        Assert.Contains("Nectar held                      2", lines);
        Assert.Contains("God Mode level                   0", lines);
        Assert.Contains("Lifetime Darkness (header)       198", lines);
        Assert.Contains("God Mode on                      False", lines);
    }

    [Fact]
    public void Show_prints_hades2_values()
    {
        var lines = Show(TestData.Hades2);

        Assert.Contains("Game                             Hades 2", lines);
        Assert.Contains("Bones held                       3785", lines);
        Assert.Contains("Ashes held                       760", lines);
        Assert.Contains("Psyche held                      934", lines);
        Assert.Contains("Nectar held                      6", lines);
        Assert.Contains("God Mode level                   0", lines);
        Assert.Contains("God Mode on                      False", lines);
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
    public void Set_edits_the_save_in_place_after_writing_a_verified_backup()
    {
        File.Copy(TestData.Hades1, Input);
        var original = File.ReadAllBytes(Input);
        var output = new StringWriter();

        var exitCode = Cli.Run(["set", Input, "--keys", "50", "--godmode-level", "30"], output, TextWriter.Null, BackupDir);

        Assert.Equal(0, exitCode);
        var written = HadesSave.Read(File.ReadAllBytes(Input));
        var fields = GameFields.ByGame[Game.Hades1];
        Assert.Equal(50, fields.Single(f => f.Name == "keys").Get(written));
        Assert.Equal(30, fields.Single(f => f.Name == "godmode-level").Get(written));
        Assert.Equal(10888, fields.Single(f => f.Name == "darkness").Get(written));

        var backup = Assert.Single(Directory.GetFiles(BackupDir));
        Assert.Matches(@"^Hades1-in-\d{8}-\d{6}\.sav$", Path.GetFileName(backup));
        Assert.Equal(original, File.ReadAllBytes(backup));
        Assert.Equal([Input], Directory.GetFiles(_dir));
        Assert.Contains($"Backup saved to {backup}", output.ToString());
        Assert.Contains($"Saved to {Input}", output.ToString());
    }

    [Fact]
    public void Backup_name_carries_the_game_and_the_save_file_name()
    {
        var input = Path.Combine(_dir, "Profile3.sav");
        File.Copy(TestData.Hades2, input);

        var exitCode = Cli.Run(["set", input, "--bones", "1"], TextWriter.Null, TextWriter.Null, BackupDir);

        Assert.Equal(0, exitCode);
        var backup = Assert.Single(Directory.GetFiles(BackupDir));
        Assert.Matches(@"^Hades2-Profile3-\d{8}-\d{6}\.sav$", Path.GetFileName(backup));
    }

    [Fact]
    public void Repeated_in_place_edits_never_overwrite_an_earlier_backup()
    {
        File.Copy(TestData.Hades1, Input);
        var original = File.ReadAllBytes(Input);

        Assert.Equal(0, Cli.Run(["set", Input, "--keys", "1"], TextWriter.Null, TextWriter.Null, BackupDir));
        var afterFirst = File.ReadAllBytes(Input);
        Assert.Equal(0, Cli.Run(["set", Input, "--keys", "2"], TextWriter.Null, TextWriter.Null, BackupDir));
        var afterSecond = File.ReadAllBytes(Input);
        Assert.Equal(0, Cli.Run(["set", Input, "--keys", "3"], TextWriter.Null, TextWriter.Null, BackupDir));

        var backups = Directory.GetFiles(BackupDir).Select(File.ReadAllBytes).ToList();
        Assert.Equal(3, backups.Count);
        Assert.Contains(backups, b => b.AsSpan().SequenceEqual(original));
        Assert.Contains(backups, b => b.AsSpan().SequenceEqual(afterFirst));
        Assert.Contains(backups, b => b.AsSpan().SequenceEqual(afterSecond));
        Assert.Equal(3, GameFields.ByGame[Game.Hades1].Single(f => f.Name == "keys").Get(HadesSave.Read(File.ReadAllBytes(Input))));
    }

    [Fact]
    public void A_backup_name_already_taken_gets_a_suffix_and_the_existing_file_is_left_alone()
    {
        File.Copy(TestData.Hades1, Input);
        var original = File.ReadAllBytes(Input);
        Directory.CreateDirectory(BackupDir);
        var now = DateTime.Now;
        // Occupy the names for this second and the next two, so the run collides whichever second it lands in.
        var taken = Enumerable.Range(0, 3).Select(i => Path.Combine(BackupDir, $"Hades1-in-{now.AddSeconds(i):yyyyMMdd-HHmmss}.sav")).ToList();
        foreach (var file in taken)
            File.WriteAllBytes(file, new byte[] { 1, 2, 3 });

        var exitCode = Cli.Run(["set", Input, "--keys", "5"], TextWriter.Null, TextWriter.Null, BackupDir);

        Assert.Equal(0, exitCode);
        foreach (var file in taken)
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(file));
        var added = Assert.Single(Directory.GetFiles(BackupDir).Except(taken));
        Assert.EndsWith("-1.sav", added);
        Assert.Equal(original, File.ReadAllBytes(added));
    }

    [Fact]
    public void Set_with_out_writes_a_new_file_and_makes_no_backup()
    {
        File.Copy(TestData.Hades1, Input);
        var original = File.ReadAllBytes(Input);
        var output = new StringWriter();

        var exitCode = Cli.Run(["set", Input, "--out", Output, "--keys", "50", "--godmode-level", "30"], output, TextWriter.Null, BackupDir);

        Assert.Equal(0, exitCode);
        Assert.Equal(original, File.ReadAllBytes(Input));
        Assert.Equal(2, Directory.GetFiles(_dir).Length);
        Assert.False(Directory.Exists(BackupDir));
        var written = HadesSave.Read(File.ReadAllBytes(Output));
        var fields = GameFields.ByGame[Game.Hades1];
        Assert.Equal(50, fields.Single(f => f.Name == "keys").Get(written));
        Assert.Equal(30, fields.Single(f => f.Name == "godmode-level").Get(written));
        Assert.Equal(10888, fields.Single(f => f.Name == "darkness").Get(written));
        Assert.DoesNotContain("Backup", output.ToString());
        Assert.Contains($"Saved to {Output}", output.ToString());
    }

    [Fact]
    public void Environment_variables_in_paths_are_expanded()
    {
        // PowerShell passes %VAR% through literally, so the app must expand it for the README paths to work there.
        File.Copy(TestData.Hades1, Input);
        Environment.SetEnvironmentVariable("HADES_EDITOR_TEST_DIR", _dir);
        var dir = $"%HADES_EDITOR_TEST_DIR%{Path.DirectorySeparatorChar}";

        Assert.Contains("Chthonic Keys held               6", Show(dir + "in.sav"));
        var exitCode = Cli.Run(["set", dir + "in.sav", "--out", dir + "out.sav", "--keys", "50"], TextWriter.Null, TextWriter.Null, BackupDir);

        Assert.Equal(0, exitCode);
        Assert.Equal(50, GameFields.ByGame[Game.Hades1].Single(f => f.Name == "keys").Get(HadesSave.Read(File.ReadAllBytes(Output))));
    }

    private string[] Show(string path)
    {
        var output = new StringWriter();
        var exitCode = Cli.Run(["show", path], output, TextWriter.Null, BackupDir);
        Assert.Equal(0, exitCode);
        return output.ToString().Split(Environment.NewLine);
    }
}
