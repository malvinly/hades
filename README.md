# hades1-editor

A small command-line tool that edits Darkness, Chthonic Keys and the God Mode level in a Hades 1 save file (`.sav`, save version 16).

Every byte that is not part of the requested edit is written back unchanged. The parser keeps each Lua table's entry order and original size fields, copies numbers as their exact 8 bytes, and reproduces the game's LZ4 output. Only the header checksum is recalculated.

## Usage

```
hades1-editor show <save.sav>
hades1-editor set <save.sav> --out <new.sav> [--darkness N] [--keys N] [--godmode-level N]
hades1-editor set <save.sav> --in-place [--darkness N] [--keys N] [--godmode-level N]
```

`--in-place` writes a timestamped backup next to the save before editing it. Otherwise the input file is never written to.

`--godmode-level` accepts 0 to 30. The game caps the level at 30. The God Mode on/off switch is left alone; turn it on in the game.

## Layout

- `src/Hades.SaveFormat`: save file, luabins and LZ4 handling, shared with a future Hades 2 editor
- `src/Hades1-Editor`: the CLI and the list of Hades 1 fields it can edit
- `tests/Hades1-Editor.Tests`: xUnit tests. They need a copy of a Hades 1 save at `tests/Hades1-Editor.Tests/TestData/Profile1.sav`, which is gitignored.

## Build and test

```
dotnet test
dotnet run --project src/Hades1-Editor -- show path/to/Profile1.sav
```
