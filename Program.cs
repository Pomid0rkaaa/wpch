using System.Runtime.InteropServices;

static class WallpaperChanger
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SystemParametersInfo(int uAction, int uParam, string lvpParam, int fuWinIni);

    private const int SPI_SETDESKWALLPAPER = 0x0014;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDWININICHANGE = 0x02;

    public static bool SetWallpaper(string path) =>
        SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE) != 0;
}

class Program
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static string[] _wallpapers = [];
    private static string? _listPath;
    private static string? _filter;
    private static string? _imgURL;
    private static TimeSpan? _interval;

    static async Task<int> Main(string[] args)
    {
        if (!ParseArgs(args))
        {
            Console.Error.WriteLine("Invalid arguments.");
            PrintHelp();
            return 1;
        }
        if (_imgURL is not null)
        {
            return await RunOnceErrors();
        }

        _listPath ??= Path.Combine(AppContext.BaseDirectory, "wallpapers.txt");
        if (!File.Exists(_listPath))
        {
            Console.Error.WriteLine($"{_listPath} was not found");
            return 1;
        }

        var query = (await File.ReadAllLinesAsync(_listPath))
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith('#'));

        if (!string.IsNullOrEmpty(_filter))
        {
            query = query.Where(x => x.Contains(_filter, StringComparison.OrdinalIgnoreCase));
        }

        _wallpapers = [.. query];

        if (_wallpapers.Length == 0)
        {
            Console.Error.WriteLine($"{_listPath} is empty");
            return 1;
        }

        if (_interval is null)
        {
            return await RunOnceErrors();
        }

        var next = DateTime.UtcNow + _interval.Value;
        while (true)
        {
            await RunOnceErrors();

            next += _interval.Value;
            var delay = next - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay);
            else
                next = DateTime.UtcNow;
        }
    }

    static bool ParseArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--img":
                    if (_imgURL is not null)
                    {
                        Console.Error.WriteLine("Image specified more than once.");
                        return false;
                    }
                    if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
                    {
                        Console.Error.WriteLine("Missing value for picture argument.");
                        return false;
                    }
                    _imgURL = args[++i];
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
                case "--interval":
                case "-i":
                    if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
                    {
                        Console.Error.WriteLine("Missing value for interval argument.");
                        return false;
                    }
                    if (_interval is not null)
                    {
                        Console.Error.WriteLine("Interval specified more than once.");
                        return false;
                    }

                    try
                    {
                        _interval = ParseTime(args[++i]);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Invalid interval argument: {ex.Message}");
                        return false;
                    }
                    break;

                case "--list":
                case "-l":
                    if (_listPath is not null)
                    {
                        Console.Error.WriteLine("List specified more than once.");
                        return false;
                    }
                    if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
                    {
                        Console.Error.WriteLine("Missing value for list argument.");
                        return false;
                    }
                    _listPath = args[++i];
                    break;
                case "--has":
                    if (_filter is not null)
                    {
                        Console.Error.WriteLine("Filter specified more than once.");
                        return false;
                    }
                    if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
                    {
                        Console.Error.WriteLine("Missing value for filter argument.");
                        return false;
                    }
                    _filter = args[++i];
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    return false;
            }
        }
        return true;
    }

    static void PrintHelp()
    {
        Console.WriteLine("""
Usage:
  wpch [options]

Options:
  -l, --list <path>     Path to wallpapers list file
  -i, --interval <time> Interval (e.g. 10s, 5m, 1h)
      --has <text>      Filter wallpapers containing text
  -h, --help            Show help
      --img <url>       Set wallpaper from a specific URL

Examples:
    wpch -l wallpapers.txt -i 10m
    wpch --has cat --interval 5m
""");
    }

    static async Task<int> RunOnceErrors()
    {
        try
        {
            return await RunOnce();
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Permission denied while accessing files");
            return 1;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Failed to fetch wallpaper: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            return 1;
        }
    }

    static async Task<int> RunOnce()
    {
        string selected = _imgURL ?? _wallpapers[Random.Shared.Next(_wallpapers.Length)];

        if (!Uri.TryCreate(selected, UriKind.Absolute, out _))
        {
            Console.Error.WriteLine($"Invalid URL: {selected}");
            return 1;
        }

        using var response = await GetRetry(selected);

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is not ("image/jpeg" or "image/jpg" or "image/png" or "image/bmp"))
        {
            Console.Error.WriteLine($"Unsupported wallpaper MIME type: {contentType}");
            return 1;
        }

        string extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/bmp" => ".bmp",
            _ => throw new InvalidOperationException($"Unsupported MIME type: {contentType}")
        };

        string wallpaperPath = Path.Combine(AppContext.BaseDirectory, $"wallpaper{extension}");
        await using (var stream = await response.Content.ReadAsStreamAsync())
        await using (var fileStream = new FileStream(
            wallpaperPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None))
        {
            await stream.CopyToAsync(fileStream);
            await fileStream.FlushAsync();
        }

        if (!WallpaperChanger.SetWallpaper(wallpaperPath))
        {
            Console.Error.WriteLine("Failed to set wallpaper");
            return 1;
        }
        return 0;
    }

    static async Task<HttpResponseMessage> GetRetry(string url, int retries = 5)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                var response = await Http.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead
                );

                if ((int)response.StatusCode >= 500)
                {
                    response.Dispose();

                    if (attempt < retries)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
                        continue;
                    }
                }

                if (!response.IsSuccessStatusCode)
                {
                    var status = response.StatusCode;
                    response.Dispose();
                    throw new HttpRequestException($"HTTP {(int)status}");
                }
                return response;
            }
            catch (Exception ex) when (
                ex is HttpRequestException or TaskCanceledException)
            {
                lastException = ex;

                if (attempt < retries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
                    continue;
                }
            }
        }

        throw lastException ?? new Exception("Download failed");
    }

    static TimeSpan ParseTime(string input)
    {
        if (input.Length < 2)
            throw new ArgumentException("Invalid interval format");

        char unit = input[^1];
        if (!int.TryParse(input[..^1], out int value))
            throw new ArgumentException("Invalid interval value");

        if (value <= 0)
            throw new ArgumentException("Interval must be greater than zero");

        return unit switch
        {
            's' => TimeSpan.FromSeconds(value),
            'm' => TimeSpan.FromMinutes(value),
            'h' => TimeSpan.FromHours(value),
            _ => throw new ArgumentException("Invalid interval unit, use s/m/h")
        };
    }
}