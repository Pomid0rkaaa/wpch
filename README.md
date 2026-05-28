# Wallpaper Changer

A lightweight Windows CLI tool that randomly changes your desktop wallpaper from a list of image URLs.

## Features

- Random wallpaper selection
- Interval mode (s / m / h)
- Retry on failed downloads
- Simple `wallpapers.txt` config
- No dependencies besides .NET runtime
- Low memory usage

## Usage

### Run once

```bash
wpch.exe
```

### Run with interval

```bash
wpch.exe --interval 10m
```

Supports:

- `s` = seconds
- `m` = minutes
- `h` = hours

Example:

```bash
--interval 30s
--interval 5m
--interval 1h
```

## Configuration

Create a `wallpapers.txt` file in the same folder:

```txt
https://example.com/image1.jpg
https://example.com/image2.png
# comments are allowed
```

## Supported formats

- JPG / JPEG
- PNG
- BMP

## Notes

- Requires internet access for remote wallpapers
- Runs on Windows only (uses SystemParametersInfo API)
- Keeps memory usage stable by design
