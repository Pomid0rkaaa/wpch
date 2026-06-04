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

    private static bool _verbose;
    private static bool _showTitle;
    private static bool _dryRun;
    private static bool _countOnly;
    private static bool _shuffle;
    private static Queue<string> _shuffleQueue = new();
    private static Random _random = Random.Shared;
    private static int? _seed;
    private static string? _listPath;
    private static string? _filter;
    private static string? _imgURL;
    private static string[] _wallpapers = [];
    private static TimeSpan? _interval;

    static void Log(string message)
    {
        if (_verbose)
            Console.WriteLine(message);
    }

    static async Task<int> Main(string[] args)
    {
        if (!ParseArgs(args))
        {
            Console.Error.WriteLine("Invalid arguments.");
            PrintHelp();
            return 1;
        }
        if (_seed is not null)
            _random = new Random(_seed.Value);

        if (_imgURL is not null)
            return await RunOnceErrors();

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

        if (_countOnly)
        {
            Console.WriteLine($"Found {_wallpapers.Length} matching wallpapers");
            return 0;
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
        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--title":
                    case "-t":
                        _showTitle = true;
                        break;
                    case "--verbose":
                    case "-v":
                        _verbose = true;
                        break;
                    case "--dry-run":
                        _dryRun = true;
                        break;
                    case "--count":
                        _countOnly = true;
                        break;
                    case "--shuffle":
                        _shuffle = true;
                        break;
                    case "--help":
                    case "-h":
                        PrintHelp();
                        Environment.Exit(0);
                        break;
                    case "--version":
                        Console.WriteLine("wpch v1.2");
                        Environment.Exit(0);
                        break;
                    case "--seed":
                        if (_seed is not null)
                        {
                            Console.Error.WriteLine("Seed specified more than once.");
                            return false;
                        }

                        if (!int.TryParse(RequireValue(args, ref i, "seed"), out int seed))
                        {
                            Console.Error.WriteLine("Invalid seed.");
                            return false;
                        }

                        _seed = seed;
                        break;
                    case "--img":
                        if (_imgURL is not null)
                        {
                            Console.Error.WriteLine("Image specified more than once.");
                            return false;
                        }
                        _imgURL = RequireValue(args, ref i, "image");
                        break;
                    case "--interval":
                    case "-i":
                        if (_interval is not null)
                        {
                            Console.Error.WriteLine("Interval specified more than once.");
                            return false;
                        }

                        try
                        {
                            _interval = ParseTime(RequireValue(args, ref i, "interval"));
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
                        _listPath = RequireValue(args, ref i, "list");
                        break;
                    case "--has":
                    case "-f":
                        if (_filter is not null)
                        {
                            Console.Error.WriteLine("Filter specified more than once.");
                            return false;
                        }
                        _filter = RequireValue(args, ref i, "filter");
                        break;
                    default:
                        Console.Error.WriteLine($"Unknown argument: {args[i]}");
                        return false;
                }
            }
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return false;
        }
        return true;
    }

    static string RequireValue(string[] args, ref int i, string name)
    {
        if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
            throw new ArgumentException($"Missing value for {name}");

        return args[++i];
    }

    static void PrintHelp()
    {
        Console.WriteLine("""
Usage:
  wpch [options]

Options:
  -l, --list <path>      Path to wallpapers list file
  -i, --interval <time>  Interval (e.g. 10s, 5m, 1h)
  -f, --has <text>       Filter wallpapers containing text
  -v, --verbose          Show download and selection details
  -t, --title            Print selected wallpaper name
  -h, --help             Show help
  -c, --count            Show number of wallpapers matching filter
  -s, --shuffle          Cycle through wallpapers without repeats
  -S, --seed <n>         Use deterministic random seed
      --dry-run          Show which wallpaper would be selected without downloading
      --img <url>        Set wallpaper from a specific URL
      --version          Print program version

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
        string selected;

        if (_imgURL is not null)
        {
            selected = _imgURL;
        }
        else if (_shuffle)
        {
            if (_shuffleQueue.Count == 0)
            {
                RefillShuffleQueue();
            }

            selected = _shuffleQueue.Dequeue();
        }
        else
        {
            selected = _wallpapers[_random.Next(_wallpapers.Length)];
        }

        if (_dryRun)
        {
            Console.WriteLine($"[Dry Run] Selected wallpaper: {selected}");
            if (_showTitle)
            {
                Console.WriteLine(Path.GetFileNameWithoutExtension(new Uri(selected).AbsolutePath));
            }
            return 0;
        }

        Log($"Selected wallpaper: {selected}");

        if (!Uri.TryCreate(selected, UriKind.Absolute, out _))
        {
            Console.Error.WriteLine($"Invalid URL: {selected}");
            return 1;
        }
        else if (_showTitle)
        {
            Console.WriteLine(
                Path.GetFileNameWithoutExtension(
                    new Uri(selected).AbsolutePath));
        }

        using var response = await GetRetry(selected);

        var contentType = response.Content.Headers.ContentType?.MediaType;
        Log($"Content-Type: {contentType}");
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
        Log($"Saving to {wallpaperPath}");
        await using (var stream = await response.Content.ReadAsStreamAsync())
        await using (var fileStream = new FileStream(
            wallpaperPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None))
        {
            long? totalBytes = response.Content.Headers.ContentLength;

            byte[] buffer = new byte[81920];
            long downloaded = 0;
            int read;
            int lastPercent = -1;

            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));

                downloaded += read;

                if (_verbose && totalBytes is not null)
                {
                    int percent = (int)(downloaded * 100 / totalBytes.Value);

                    if (percent != lastPercent)
                    {
                        Console.Write(
                            $"\rDownloading: {percent}% " +
                            $"({FormatSize(downloaded)} / {FormatSize(totalBytes.Value)})"
                        );

                        lastPercent = percent;
                    }
                }
            }
            if (_verbose && totalBytes is not null)
            {
                Console.WriteLine();
            }
            await fileStream.FlushAsync();
        }

        Log("Setting wallpaper");
        if (!WallpaperChanger.SetWallpaper(wallpaperPath))
        {
            Console.Error.WriteLine("Failed to set wallpaper");
            return 1;
        }
        Log("Done!");
        return 0;
    }

    static void RefillShuffleQueue()
    {
        var items = _wallpapers.ToArray();

        for (int i = items.Length - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }

        _shuffleQueue = new Queue<string>(items);
    }

    static string FormatSize(long bytes)
    {
        const double KB = 1024;
        const double MB = KB * 1024;
        const double GB = MB * 1024;

        return bytes switch
        {
            >= (long)GB => $"{bytes / GB:F1} GB",
            >= (long)MB => $"{bytes / MB:F1} MB",
            >= (long)KB => $"{bytes / KB:F1} KB",
            _ => $"{bytes} B"
        };
    }

    static async Task<HttpResponseMessage> GetRetry(string url, int retries = 5)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                Log($"Download attempt {attempt}/{retries}");
                var response = await Http.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead
                );
                Log($"Server returned {(int)response.StatusCode}");
                if ((int)response.StatusCode >= 500)
                {
                    response.Dispose();

                    if (attempt < retries)
                    {
                        Log("Retrying...");
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
                    Log($"Retrying after error: {ex.Message}");
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