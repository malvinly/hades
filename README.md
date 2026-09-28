# hades-editor

Built for organizing Hades speedrun competitions: organizers edit one base save into each scenario's starting file and distribute it to competitors.

It is a command-line tool that edits a few values in a Hades 1 or Hades II save file (`.sav`). The game is detected from the file.

| Game | Save version | Fields |
| --- | --- | --- |
| Hades 1 | 16 | Darkness held, Chthonic Keys held, God Mode level |
| Hades II | 18 | Bones held, Ashes held, Psyche held, God Mode level |

God Mode level accepts 0 to 30; both games cap it at 30. The God Mode on/off switch is left alone; turn it on in the game.

Every header field and every Lua value other than the ones you edit is written back unchanged; only the checksum and the compressed block change. Before editing, the tool checks that it can reproduce the input file byte for byte and refuses if it cannot.

## Requirements

- Windows
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) to build

## Build

From the repository root:

```
dotnet build -c Release
```

The executable is written to:

```
src\Hades-Editor\bin\Release\net10.0\hades-editor.exe
```

Run the tests with `dotnet test`. They need copies of real saves in `tests\Hades-Editor.Tests\TestData\`, which is gitignored:

- `Hades1-Profile1.sav`: a Hades 1 save
- `Hades2-Profile1.sav`: a Hades II save

## Usage

Close the game first. It rewrites the save file when you exit.

Save locations:

- Hades 1: `%USERPROFILE%\Documents\Saved Games\Hades\Profile1.sav`
- Hades II: `%USERPROFILE%\Saved Games\Hades II\Profile1.sav`

### Show the current values

```
hades-editor show "%USERPROFILE%\Documents\Saved Games\Hades\Profile1.sav"
```

Prints the game, the editable values, and whether God Mode is on. For Hades 1 it also prints the lifetime Darkness stored in the header. A value the save does not contain yet is shown as missing.

### Edit into a new file

```
hades-editor set "%USERPROFILE%\Documents\Saved Games\Hades\Profile1.sav" --out edited.sav --darkness 20000 --keys 30 --godmode-level 10
```

```
hades-editor set "%USERPROFILE%\Saved Games\Hades II\Profile1.sav" --out edited.sav --bones 5000 --ashes 1000 --psyche 250
```

Any combination of the game's fields works. The input file is never written to, and `--out` must name a different file. The output is written to a temporary file and moved into place, so a failed write never leaves a partial save behind.

### Errors

The tool writes nothing and exits with code 1 when:

- the file is not a Hades save, or its checksum does not match
- the save version is not 16 (Hades 1) or 18 (Hades II)
- a field belongs to the other game, for example `--bones` on a Hades 1 save
- a value is missing from the save, negative, not a whole number, or a God Mode level above 30
- `--out` is missing, names the input file, or an option is repeated
- the save does not round-trip byte for byte before editing, which means the file contains something this tool cannot reproduce exactly

After every edit the output is re-read and checked for the new values before the tool reports success.

## Layout

- `src\Hades.SaveFormat`: save file, luabins and LZ4 handling for both games
- `src\Hades-Editor`: the CLI and the per-game field tables
- `tests\Hades-Editor.Tests`: xUnit tests
