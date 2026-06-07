# Wallpaper Changer

A lightweight Windows CLI tool that randomly changes your desktop wallpaper from a list of image URLs.

## Features

- Random or deterministic wallpaper selection (seeded shuffle supported)
- Accept lists or single URLs via file or standard input (stdin)
- Interval mode
- Dry-run mode to preview selection without downloading
- Case-insensitive filtering
- Verbose output
- Single instance lock (prevents concurrent execution)
- Automatic retry on failed downloads

## Quick reference

| Short | Long | Description |
| ----- | ----------------------- | ---------------------------------------------------------------------- |
| -l | --list &lt;path&gt; | Path to wallpaper URL (or `stdin` / `-`) |
| -i | --interval &lt;time&gt; | Interval mode (supports s, m, h). Runs continuously until stopped (Ctrl+C). The first wallpaper is applied immediately; subsequent changes follow the interval. |
| -f | | Filter wallpapers by comma-separated substrings. Prefix - to exclude (e.g. nature,-flower). Excludes take priority over includes |
| | --include | Filter URLs including comma-separated substring (case-insensitive) |
| | --exclude | Filter URLs excluding comma-separated substring (case-insensitive) |
| -v | --verbose | Print download and selection details |
| -t | --title | Print selected wallpaper name |
| -c | --count | Print number of wallpapers matching filter |
| -L | --list-all | List wallpapers after applying filters |
| -s | --shuffle | Cycle through wallpapers without repeats |
| -S | --seed &lt;n&gt; | Use deterministic random seed |
| -h | --help | Print help |
| -V | --version | Print program version |
| -d | --dry-run | Print selected wallpaper without downloading or applying it |
| -I | --img &lt;URL&gt; | Set wallpaper from a specific URL |

## Installation **(Windows-only)**

Download the latest release and place `wpch.exe` anywhere on your system

Or build from source:

```powershell
dotnet publish
```

## Common examples

```powershell
# Basic
wpch.exe -l wallpapers.txt

# Filtered rotation
wpch.exe -f nature,-flower -i 10m

# Deterministic test
wpch.exe -L -s -S 420 -t
```

## Execution order

- `--img` overrides all other inputs
- Command-line list overrides `wallpapers.txt`
- Filters apply after loading URLs
- Exclusions override inclusions
- `--dry-run` overrides wallpaper application but still evaluates selection logic
- `--seed` affects deterministic ordering before selection
- `--shuffle` enforces sequential cycling instead of random selection

## Configuration

Create a `wallpapers.txt` file in the same folder:

```txt
https://example.com/image1.jpg
https://example.com/image2.png
# comments are allowed
```

Default location: executable directory

## Supported formats

File type validation is performed using HTTP MIME types (not file extensions)

- JPG / JPEG
- PNG
- BMP

## Notes

- Windows only
- Requires internet access
- Uses a system-wide Mutex to ensure only one instance runs at a time
- Uses retry logic for unreliable connections
- Automatically handles filesystem cleanup by deleting the previous temporary wallpaper file before downloading a new one
- Low memory usage
