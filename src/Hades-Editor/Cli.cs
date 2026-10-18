using Hades.SaveFormat;

namespace HadesEditor;

public static class Cli
{
    private static readonly string Usage = $"""
        Usage:
          hades-editor show <save.sav>
          hades-editor set <save.sav> [--<field> N]...
          hades-editor set <save.sav> --out <new.sav> [--<field> N]...

        The game is detected from the save file. Fields:
        {string.Join("\n", GameFields.ByGame.Select(g => $"  {GameFields.Label(g.Key)}:\n" +
            string.Join("\n", g.Value.Select(f => $"    --{f.Name,-14} {f.Description} ({f.Min}-{f.Max})"))))}

        Without --out the save is edited in place. A copy of the original is written to the hades-backup
        folder next to hades-editor.exe first, and the save is left untouched if that copy fails.
        With --out a new file is written and the input is never touched. The output path must be a different file.
        """;

    /// <param name="backupDir">Folder that receives a copy of every save edited in place.</param>
    public static int Run(string[] args, TextWriter output, TextWriter error, string backupDir)
    {
        try
        {
            var options = ParseOptions(args.Skip(2));
            return (args.ElementAtOrDefault(0), args.ElementAtOrDefault(1)) switch
            {
                ("show", { } path) when options.Count == 0 => Show(Environment.ExpandEnvironmentVariables(path), output),
                ("set", { } path) => Set(Environment.ExpandEnvironmentVariables(path), options, output, backupDir),
                _ => throw new ArgumentException(Usage),
            };
        }
        catch (Exception e) when (e is SaveFormatException or ArgumentException or IOException or UnauthorizedAccessException)
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
            output.WriteLine($"{field.Description,-32} {field.Find(save)?.ToString() ?? "missing (not yet in this save)"}");
        if (save.Game == Game.Hades1)
            output.WriteLine($"{"Lifetime Darkness (header)",-32} {save.AccumulatedMetaPoints}");
        output.WriteLine($"{"God Mode on",-32} {save.EasyMode != 0}");
        return 0;
    }

    private static int Set(string path, Dictionary<string, string> options, TextWriter output, string backupDir)
    {
        var inPlace = !options.Remove("out", out var outOption);
        var outPath = path;
        if (!inPlace)
        {
            outPath = Environment.ExpandEnvironmentVariables(outOption!);
            if (string.Equals(Path.GetFullPath(outPath), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Output path must be a different file from the input");
        }
        if (options.Count == 0)
            throw new ArgumentException("Nothing to set\n" + Usage);

        var input = File.ReadAllBytes(path);
        var save = HadesSave.Read(input);
        if (!save.Write().AsSpan().SequenceEqual(input))
            throw new SaveFormatException("This save does not round-trip byte for byte; refusing to edit it");

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

        string? backupPath = null;
        if (inPlace)
        {
            backupPath = WriteBackup(backupDir, path, save.Game, input);
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(input))
                throw new IOException($"{path} changed on disk after it was read. The save was not modified; the copy read earlier is at {backupPath}");
        }
        try
        {
            WriteAtomically(outPath, bytes);
        }
        catch (Exception e) when (backupPath != null && e is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"{e.Message} The save was not modified; its backup is at {backupPath}", e);
        }

        foreach (var (field, newValue) in edits)
            output.WriteLine($"{field.Description,-32} {newValue}");
        if (backupPath != null)
            output.WriteLine($"Backup saved to {backupPath}");
        output.WriteLine($"Saved to {outPath}");
        return 0;
    }

    /// <summary>
    /// Copies the original bytes of a save into the backup folder and reads them back to confirm the copy is intact.
    /// Nothing about the save is touched until this has succeeded. Names carry a timestamp to the second, so a second
    /// edit within the same second gets a numeric suffix instead of overwriting the earlier backup.
    /// </summary>
    private static string WriteBackup(string backupDir, string savePath, Game game, byte[] original)
    {
        try
        {
            Directory.CreateDirectory(backupDir);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"Could not create the backup folder {backupDir} ({e.Message}). The save was not modified");
        }

        var stem = $"{game}-{Path.GetFileNameWithoutExtension(savePath)}-{DateTime.Now:yyyyMMdd-HHmmss}";
        for (var attempt = 0; ; attempt++)
        {
            var backupPath = Path.Combine(backupDir, attempt == 0 ? $"{stem}.sav" : $"{stem}-{attempt}.sav");
            FileStream stream;
            try
            {
                stream = new FileStream(backupPath, FileMode.CreateNew);
            }
            catch (IOException) when (File.Exists(backupPath) && attempt < 100)
            {
                continue;
            }
            try
            {
                using (stream)
                {
                    stream.Write(original);
                    stream.Flush(flushToDisk: true);
                }
                if (File.ReadAllBytes(backupPath).AsSpan().SequenceEqual(original))
                    return backupPath;
                throw new IOException("it did not read back intact");
            }
            catch (IOException e)
            {
                File.Delete(backupPath);
                throw new IOException($"Could not write the backup {backupPath} ({e.Message}). The save was not modified", e);
            }
        }
    }

    /// <summary>
    /// Writes to a uniquely named temp file in the target folder, then moves it into place, so a failed
    /// write never leaves a partial output and the temp file can never be the input or an existing file.
    /// </summary>
    private static void WriteAtomically(string outPath, byte[] bytes)
    {
        var tempPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outPath))!, Path.GetRandomFileName());
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(tempPath, outPath, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>Parses "--name value" pairs. Every option takes a value; repeating one is an error.</summary>
    private static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
    {
        var options = new Dictionary<string, string>();
        var list = args.ToList();
        for (var i = 0; i < list.Count; i += 2)
        {
            if (!list[i].StartsWith("--") || i + 1 >= list.Count)
                throw new ArgumentException($"Expected --option value, got '{list[i]}'\n{Usage}");
            if (!options.TryAdd(list[i][2..], list[i + 1]))
                throw new ArgumentException($"{list[i]} was given more than once");
        }
        return options;
    }
}
