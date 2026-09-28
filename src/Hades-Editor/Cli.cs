using Hades.SaveFormat;

namespace HadesEditor;

public static class Cli
{
    private static readonly string Usage = $"""
        Usage:
          hades-editor show <save.sav>
          hades-editor set <save.sav> (--out <new.sav> | --in-place) [--<field> N]...

        The game is detected from the save file. Fields:
        {string.Join("\n", GameFields.ByGame.Select(g => $"  {GameFields.Label(g.Key)}:\n" +
            string.Join("\n", g.Value.Select(f => $"    --{f.Name,-14} {f.Description} ({f.Min}-{f.Max})"))))}

        --in-place writes a timestamped backup next to the save before editing it.
        """;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            var options = ParseOptions(args.Skip(2));
            return (args.ElementAtOrDefault(0), args.ElementAtOrDefault(1)) switch
            {
                ("show", { } path) when options.Count == 0 => Show(path, output),
                ("set", { } path) => Set(path, options, output),
                _ => throw new ArgumentException(Usage),
            };
        }
        catch (Exception e) when (e is SaveFormatException or ArgumentException or IOException)
        {
            error.WriteLine($"Error: {e.Message}");
            return 1;
        }
    }

    private static int Show(string path, TextWriter output)
    {
        var save = HadesSave.Read(File.ReadAllBytes(path));
        output.WriteLine($"{"Game",-32} {GameFields.Label(save.Game)}");
        foreach (var field in GameFields.ByGame[save.Game])
            output.WriteLine($"{field.Description,-32} {field.Get(save)}");
        if (save.Game == Game.Hades1)
            output.WriteLine($"{"Lifetime Darkness (header)",-32} {save.AccumulatedMetaPoints}");
        output.WriteLine($"{"God Mode on (header)",-32} {save.EasyMode != 0}");
        if (save.RootTable.Find(["ConfigOptionCache", "EasyMode"]) is LuaBool luaEasyMode)
            output.WriteLine($"{"God Mode on (ConfigOptionCache)",-32} {luaEasyMode.Value}");
        return 0;
    }

    private static int Set(string path, Dictionary<string, string?> options, TextWriter output)
    {
        var inPlace = options.Remove("in-place");
        var outPath = options.Remove("out", out var value) ? value : null;
        if (inPlace == (outPath is not null))
            throw new ArgumentException("Specify exactly one of --out <path> or --in-place");
        outPath ??= path;
        if (!inPlace && Path.GetFullPath(outPath) == Path.GetFullPath(path))
            throw new ArgumentException("Output path equals input path; use --in-place to edit the file itself");
        if (options.Count == 0)
            throw new ArgumentException("Nothing to set\n" + Usage);

        var save = HadesSave.Read(File.ReadAllBytes(path));
        var fields = GameFields.ByGame[save.Game];
        var edits = options.Select(o => (
            Field: fields.FirstOrDefault(f => f.Name == o.Key)
                ?? throw new ArgumentException($"--{o.Key} is not a {GameFields.Label(save.Game)} field\n{Usage}"),
            Value: long.TryParse(o.Value, out var n) ? n : throw new ArgumentException($"--{o.Key} needs a whole number, got '{o.Value}'"))).ToList();

        foreach (var (field, newValue) in edits)
            field.Set(save, newValue);
        var bytes = save.Write();

        var reread = HadesSave.Read(bytes);
        foreach (var (field, newValue) in edits)
            if (field.Get(reread) != newValue)
                throw new SaveFormatException($"Verification failed: {field.Name} did not read back as {newValue}");

        if (inPlace)
        {
            var backup = $"{path}.{DateTime.Now:yyyyMMdd-HHmmss}.bak";
            File.Copy(path, backup);
            output.WriteLine($"Backup written to {backup}");
        }
        File.WriteAllBytes(outPath, bytes);
        foreach (var (field, newValue) in edits)
            output.WriteLine($"{field.Description,-32} {newValue}");
        output.WriteLine($"Saved to {outPath}");
        return 0;
    }

    /// <summary>Parses "--name value" pairs; a name followed by another option or nothing is a flag.</summary>
    private static Dictionary<string, string?> ParseOptions(IEnumerable<string> args)
    {
        var options = new Dictionary<string, string?>();
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            if (!list[i].StartsWith("--"))
                throw new ArgumentException($"Unexpected argument '{list[i]}'\n{Usage}");
            var hasValue = i + 1 < list.Count && !list[i + 1].StartsWith("--");
            options[list[i][2..]] = hasValue ? list[++i] : null;
        }
        return options;
    }
}
