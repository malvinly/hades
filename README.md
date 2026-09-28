# hades1-editor

Built for organizing Hades 1 speedrun competitions: organizers edit one base save into each scenario's starting file and distribute it to competitors.

It is a command-line tool that edits three values in a Hades 1 save file (`.sav`, save version 16):

- Darkness held
- Chthonic Keys held
- God Mode level (0 to 30; the game caps it at 30)

Every byte that is not part of the requested edit is written back unchanged. Only the header checksum is recalculated. The God Mode on/off switch is left alone; turn it on in the game.

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
src\Hades1-Editor\bin\Release\net10.0\hades1-editor.exe
```

Run the tests with `dotnet test`. They need a copy of a Hades 1 save at `tests\Hades1-Editor.Tests\TestData\Profile1.sav`. That folder is gitignored, so copy one in yourself.

## Usage

Close the game first. It rewrites the save file when you exit.

Hades keeps its saves in `%USERPROFILE%\Documents\Saved Games\Hades\`. The main profile is `Profile1.sav`.

### Show the current values

```
hades1-editor show "%USERPROFILE%\Documents\Saved Games\Hades\Profile1.sav"
```

Prints Darkness held, Chthonic Keys held, God Mode level, the lifetime Darkness stored in the header, and whether God Mode is on.

### Edit into a new file

```
hades1-editor set "%USERPROFILE%\Documents\Saved Games\Hades\Profile1.sav" --out edited.sav --darkness 20000 --keys 30 --godmode-level 10
```

Any combination of `--darkness`, `--keys` and `--godmode-level` works. The input file is never written to. The output path must differ from the input path.

### Edit in place

```
hades1-editor set "%USERPROFILE%\Documents\Saved Games\Hades\Profile1.sav" --in-place --godmode-level 10
```

A timestamped backup such as `Profile1.sav.20260928-134553.bak` is written next to the save before it is edited.

### Errors

The tool writes nothing and exits with code 1 when:

- the file is not a Hades save, or its checksum does not match
- the save is from Hades 2 (version 17 or later)
- a value is missing from the save, negative, not a whole number, or a God Mode level above 30
- neither or both of `--out` and `--in-place` are given

After every edit the output is re-read and checked for the new values before the tool reports success.

## Layout

- `src\Hades.SaveFormat`: save file, luabins and LZ4 handling, shared with a future Hades 2 editor
- `src\Hades1-Editor`: the CLI and the list of Hades 1 fields it can edit
- `tests\Hades1-Editor.Tests`: xUnit tests
