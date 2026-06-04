# Wallpaper Changer

A lightweight Windows CLI tool that randomly changes your desktop wallpaper from a list of image URLs.

## Features

- Random wallpaper selection from URL list
- Interval mode
- Case-insensitive filtering (`-f` / `--has`)
- Shuffle and seed support for reproducible random order (`-s` / `--shuffle`, `-S` / `--seed`)
- Dry-run mode to preview selection without downloading (`--dry-run`)
- Verbose output (`--verbose` / `-v`)
- Show wallpaper title (`--title` / `-t`)
- Count matching wallpapers (`--count` / `-c`)
- Retry logic for failed downloads
- Built-in help and version info (`--help` / `-h`, `--version`)
- Simple `wallpapers.txt` config
- Low memory usage
- Windows-only

## Quick reference (flags)

| Short | Long                    | Description                                                |
| ----- | ----------------------- | ---------------------------------------------------------- |
| -l    | --list &lt;path&gt;     | Path to wallpaper URL list                                 |
| -i    | --interval &lt;time&gt; | Change interval (`10s`, `5m`, `1h`)                        |
| -f    | --has &lt;text&gt;      | Filter URLs containing text (case-insensitive)             |
| -v    | --verbose               | Show download and selection details                        |
| -t    | --title                 | Print selected wallpaper name                              |
| -c    | --count                 | Show number of wallpapers matching filter                  |
| -s    | --shuffle               | Cycle through wallpapers without repeats                   |
| -S    | --seed &lt;n&gt;        | Use deterministic random seed                              |
| -h    | --help                  | Show help                                                  |
|       | --version               | Print program version                                      |
|       | --dry-run               | Show which wallpaper would be selected without downloading |
|       | --img &lt;URL&gt;       | Set wallpaper from a specific URL                          |

## Usage

### Run once

```powershell
wpch.exe
```

### Run with interval mode

```powershell
wpch.exe --interval 10m
wpch.exe -i 30s
```

Supports:

- `s` = seconds
- `m` = minutes
- `h` = hours

### With custom list file

```powershell
wpch.exe --list wallpapers.txt
wpch.exe -l nature.txt
```

### Filter wallpapers

```powershell
wpch.exe --has cat
wpch.exe -f dog
```

### Image right away

```powershell
wpch.exe --img https://example.com/wallpaper.jpg
```

### Combine options

```powershell
wpch.exe --list wallpapers.txt --has nature --interval 5m
```

### Shuffle / Seed

```powershell
wpch.exe --shuffle
wpch.exe -l nature.txt -s -S 420
wpch.exe --list animals.txt --seed 69
```

### Dry-run (preview selection)

```powershell
wpch.exe --dry-run
```

### Count matching wallpapers

```powershell
wpch.exe --count
wpch.exe -c --has nature
```

### Verbose output / Show title

```powershell
wpch.exe --verbose --title
wpch.exe -v
wpch.exe -t
```

## Configuration

Create a `wallpapers.txt` file in the same folder:

```txt
https://example.com/image1.jpg
https://example.com/image2.png
# comments are allowed
```

Default location: executable directory

## Supported formats

- JPG / JPEG
- PNG
- BMP

(Validated via HTTP MIME type)

## Notes

- Requires internet access for remote wallpapers
- Runs on Windows only
- Uses retry logic for unreliable connections
- Designed to minimize memory usage

## Example

```powershell
wpch.exe -l wallpapers.txt -f nature -i 10m -s -S 42 -v -t
```
