# hades-editor

Built for organizing Hades uber-casual speedruns. Looking for new, fresh, or hell mode runs? This ain't it.

Other save editors, web based and open source alike, rewrite the whole file and do not guarantee the result matches the original byte for byte.

This is a CLI tool for Hades 1 and Hades 2 save files (.sav). The game is detected from the file. Every header field and every Lua value other than the ones you edit is written back unchanged. Only the checksum and the compressed block change. Before editing, the tool checks that it can reproduce the input file byte for byte and refuses if it cannot.

## Values you can change

### Hades 1

| Option | Value | Range |
| --- | --- | --- |
| `--darkness N` | Darkness held | 0 or more |
| `--keys N` | Chthonic Keys held | 0 or more |
| `--godmode-level N` | God Mode level | 0 to 30 |

### Hades 2

| Option | Value | Range |
| --- | --- | --- |
| `--bones N` | Bones held | 0 or more |
| `--ashes N` | Ashes held | 0 or more |
| `--psyche N` | Psyche held | 0 or more |
| `--godmode-level N` | God Mode level | 0 to 30 |

Notes:

- God Mode level is the number of deaths the game counts toward damage resistance. Resistance is 20% plus 2% per level, and both games cap it at 30 (80%). It only applies while God Mode is switched on.
- The God Mode on/off switch itself is not editable here. Turn it on in the game's options.
- Lifetime totals, such as the total Darkness shown on the profile screen, are not changed. Only the amount currently held is.
- Values are whole numbers. Negative numbers and decimals are rejected.

## Usage

Close the game first. It rewrites the save file when you exit.

Save locations:

- Hades 1: `%USERPROFILE%\Documents\Saved Games\Hades\Profile1.sav`
- Hades 2: `%USERPROFILE%\Saved Games\Hades II\Profile1.sav`

### Look at a save

```
hades-editor show "%USERPROFILE%\Documents\Saved Games\Hades\Profile1.sav"
```

```
Game                             Hades 1
Darkness held                    4215
Chthonic Keys held               11
God Mode level                   4
Lifetime Darkness (header)       38420
God Mode on                      True
```

For Hades 2:

```
hades-editor show "%USERPROFILE%\Saved Games\Hades II\Profile1.sav"
```

```
Game                             Hades 2
Bones held                       1260
Ashes held                       315
Psyche held                      480
God Mode level                   2
God Mode on                      True
```

A value the save does not contain yet is shown as `missing (not yet in this save)`.

### Change values

`set` reads one save and writes a new one. It always needs `--out` with a different file name. The input is never touched.

Change one value:

```
hades-editor set "%USERPROFILE%\Documents\Saved Games\Hades\Profile1.sav" --out scenario1.sav --darkness 20000
```

Change several at once:

```
hades-editor set "%USERPROFILE%\Documents\Saved Games\Hades\Profile1.sav" --out scenario2.sav --darkness 0 --keys 0 --godmode-level 10
```

Hades 2:

```
hades-editor set "%USERPROFILE%\Saved Games\Hades II\Profile1.sav" --out scenario3.sav --bones 5000 --ashes 1000 --psyche 250
```

Build a set of scenarios from one base save:

```
hades-editor set base.sav --out no-resources.sav --darkness 0 --keys 0
hades-editor set base.sav --out rich.sav --darkness 50000 --keys 100
hades-editor set base.sav --out godmode-max.sav --godmode-level 30
```

The tool prints the values it wrote and where. Run `show` on the output to confirm.

To use a scenario file, copy it over `Profile1.sav` in the save folder while the game is closed. Keep your original somewhere safe first.

### Errors

The tool writes nothing and exits with code 1 when:

- the file is not a Hades save, or its checksum does not match
- the save version is not 16 (Hades 1) or 18 (Hades 2)
- a field belongs to the other game, for example `--bones` on a Hades 1 save
- a value is missing from the save, negative, not a whole number, or a God Mode level above 30
- `--out` is missing, names the input file, or an option is repeated
- the save does not round-trip byte for byte before editing, which means the file contains something this tool cannot reproduce exactly

After every edit the output is re-read and checked for the new values before the tool reports success. The output is written to a temporary file and moved into place, so a failed write never leaves a partial save behind.

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
