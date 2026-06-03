# Wallpaper Changer

A lightweight Windows CLI tool that randomly changes your desktop wallpaper from a list of image URLs.

## Features

- Random wallpaper selection from URL list
- Interval mode
- Case-insensitive filtering
- Retry logic for failed downloads
- Built-in help
- Simple `wallpapers.txt` config
- Low memory usage
- No dependencies besides .NET runtime
- Windows-only

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
```

### Combine options

```powershell
wpch.exe --list wallpapers.txt --has nature --interval 5m
```

## Options

| Option                      | Description                                    |
| --------------------------- | ---------------------------------------------- |
| -l, --list &lt;path&gt;     | Path to wallpaper URL list                     |
| -i, --interval &lt;time&gt; | Change interval (`10s`, `5m`, `1h`)            |
| --has &lt;text&gt;          | Filter URLs containing text (case-insensitive) |
| -h, --help                  | Show help                                      |

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
wpch --list wallpapers.txt --has nature --interval 10m
```
