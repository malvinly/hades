// Backups go next to the exe rather than next to the save: the save folder is synced by Steam Cloud and already
// holds the game's own rolling .bak files. Moving or deleting the tool's folder therefore deletes the backups too.
return HadesEditor.Cli.Run(args, Console.Out, Console.Error, Path.Combine(AppContext.BaseDirectory, "hades-backup"));
