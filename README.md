# Wallpaper Changer

A lightweight Windows CLI tool that randomly changes your desktop wallpaper from a list of image URLs.

## Features

- Random wallpaper selection from URL list
- Interval mode
- Case-insensitive filtering (`-f` / `--has`)
- Shuffle and seed support for reproducible random order (`-s` / `--shuffle`, `-S` / `--seed`)
- Dry-run mode to preview selection without downloading (`-d` / `--dry-run`)
- Verbose output (`-v` / `--verbose`)
- Show wallpaper title (`-t` / `--title`)
- Count matching wallpapers (`-c` / `--count`)
- Piping support: Accept lists or single URLs directly from standard input (`stdin`)
- Single Instance Lock: Prevents multiple background intervals from running simultaneously
- Retry logic for failed downloads
- Built-in help and version info (`-h` / `--help`, `-V` / `--version`)
- Simple `wallpapers.txt` config
- Low memory usage
- Windows-only

## Quick reference (flags)

| Short | Long                    | Description                                                |
| ----- | ----------------------- | ---------------------------------------------------------- |
| -l    | --list &lt;path&gt;     | Path to wallpaper URL (or `stdin` / `-`)                   |
| -i    | --interval &lt;time&gt; | Change interval (`10s`, `5m`, `1h`)                        |
| -f    | --has &lt;text&gt;      | Filter URLs containing text (case-insensitive)             |
| -v    | --verbose               | Show download and selection details                        |
| -t    | --title                 | Print selected wallpaper name                              |
| -c    | --count                 | Show number of wallpapers matching filter                  |
| -s    | --shuffle               | Cycle through wallpapers without repeats                   |
| -S    | --seed &lt;n&gt;        | Use deterministic random seed                              |
| -h    | --help                  | Show help                                                  |
| -V    | --version               | Print program version                                      |
| -d    | --dry-run               | Show which wallpaper would be selected without downloading |
| -I    | --img &lt;URL&gt;       | Set wallpaper from a specific URL                          |

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

### Image right away

```powershell
wpch.exe --img https://example.com/wallpaper.jpg
```

### Stream via Standart Input (stdin)

You can pass `stdin` or `-` to either `--list` or `--img` to pipe data directly into the app:

```powershell
Get-Content nature.txt | wpch.exe --list stdin
curl.exe https://example.com/wallpaper_list.txt | wpch.exe -l -
echo "https://example.com/wallpaper.jpg" | wpch.exe -I -
```

### Filter wallpapers

```powershell
wpch.exe --has cat
wpch.exe -f dog
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
wpch.exe -d
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
- Uses a system-wide Mutex to ensure only one instance runs at a time
- Automatically handles filesystem cleanup by deleting the previous temporary wallpaper file before downloading a new one
- Uses retry logic for unreliable connections
- Designed to minimize memory usage

## Example

```powershell
wpch.exe -l wallpapers.txt -f nature -i 10m -s -S 420 -v -t
```
