using wpch;

class Program
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static Arguments _config = null!;
    private static Queue<string> _shuffleQueue = new();
    private static Random _random = Random.Shared;
    private static string[] _wallpapers = [];

    static void Log(string message)
    {
        if (_config.Verbose) Console.WriteLine(message);
    }

    static async Task<int> Main(string[] args)
    {
        _config = ArgumentParser.Parse(args)!;
        if (_config == null)
        {
            Console.Error.WriteLine("Invalid arguments.\n");
            ArgumentParser.PrintHelp();
            return 1;
        }

        using Mutex mutex = new(true, "Global\\wpch_SingleInstance_Mutex", out bool isNewInstance);
        if (!isNewInstance)
        {
            Console.Error.WriteLine("wpch is already running in the background.");
            return 1;
        }

        _random = _config.Seed is int seed ? new Random(seed) : _random;

        if (_config.ImgURL is not null)
            return await RunOnceErrors();

        _wallpapers = [.. Filter(await LoadQueries())];

        if (_wallpapers.Length == 0)
        {
            Console.Error.WriteLine($"{_config.ListPath} is empty");
            return 1;
        }

        if (_config.ListAll)
        {
            static string Format(string item) =>
                _config.ShowTitle
                    ? Path.GetFileNameWithoutExtension(new Uri(item).AbsolutePath)
                    : item;

            IEnumerable<string> items =
                _config.Shuffle
                    ? new[] { RefillAndReturn() }.Concat(_shuffleQueue)
                    : _wallpapers;

            foreach (var item in items)
                Console.WriteLine(Format(item));
        }

        if (_config.CountOnly)
            Console.WriteLine($"Found {_wallpapers.Length} matching wallpapers");

        if (_config.CountOnly || _config.ListAll) return 0;

        if (_config.Interval is not TimeSpan interval)
        {
            return await RunOnceErrors();
        }

        var next = DateTime.UtcNow + interval;
        while (true)
        {
            await RunOnceErrors();
            next += interval;
            var delay = next - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay);
            else
                next = DateTime.UtcNow;
        }
    }

    private static async Task<IEnumerable<string>> LoadQueries()
    {
        if (_config.ListPath == "stdin")
            return ReadStdin();

        _config.ListPath ??= Path.Combine(AppContext.BaseDirectory, "wallpapers.txt");
        if (!File.Exists(_config.ListPath))
            throw new FileNotFoundException(_config.ListPath);

        return await File.ReadAllLinesAsync(_config.ListPath);

    }

    private static IEnumerable<string> Filter(IEnumerable<string> query)
    {
        var q = query
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith('#'));

        if (_config.Include?.Length > 0)
            q = q.Where(x => _config.Include.All(sub => x.Contains(sub, StringComparison.OrdinalIgnoreCase)));
        if (_config.Exclude?.Length > 0)
            q = q.Where(x => _config.Exclude.All(sub => !x.Contains(sub, StringComparison.OrdinalIgnoreCase)));
        return q;
    }

    static IEnumerable<string> ReadStdin()
    {
        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            yield return line;
        }
    }

    static async Task<int> RunOnceErrors()
    {
        try { return await RunOnce(); }
        catch (UnauthorizedAccessException) { Console.Error.WriteLine("Permission denied files"); return 1; }
        catch (HttpRequestException ex) { Console.Error.WriteLine($"Failed to fetch wallpaper: {ex.Message}"); return 1; }
        catch (Exception ex) { Console.Error.WriteLine($"Unexpected error: {ex.Message}"); return 1; }
    }

    static async Task<int> RunOnce()
    {
        if (_config.ImgURL == "stdin")
        {
            string? line;
            if ((line = Console.ReadLine()) != null)
            {
                _config.ImgURL = line;
            }
        }
        string selected = GetSelectedWallpaper();

        if (_config.DryRun)
        {
            Console.WriteLine($"[Dry Run] Selected wallpaper: {selected}");
            if (_config.ShowTitle) Console.WriteLine(Path.GetFileNameWithoutExtension(new Uri(selected).AbsolutePath));
            return 0;
        }

        Log($"Selected wallpaper: {selected}");
        if (!Uri.TryCreate(selected, UriKind.Absolute, out _))
        {
            Console.Error.WriteLine($"Invalid URL: {selected}");
            return 1;
        }

        if (_config.ShowTitle) Console.WriteLine(Path.GetFileNameWithoutExtension(new Uri(selected).AbsolutePath));

        using var response = await GetRetry(selected);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        Log($"Content-Type: {contentType}");

        if (contentType is null || !AllowedTypes.Contains(contentType))
        {
            Console.Error.WriteLine($"Unsupported wallpaper MIME type: {contentType}");
            return 1;
        }

        string extension = contentType switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/bmp" => ".bmp",
            _ => throw new InvalidOperationException()
        };

        string[] exts = [".jpg", ".png", ".bmp"];
        foreach (var ext in exts)
        {
            string oldFile = Path.Combine(AppContext.BaseDirectory, $"wallpaper{ext}");
            if (File.Exists(oldFile))
            {
                Log("Deleting previous wallpaper");
                try { File.Delete(oldFile); }
                catch { }
            }
        }

        string wallpaperPath = Path.Combine(AppContext.BaseDirectory, $"wallpaper{extension}");
        Log($"Saving to {wallpaperPath}");
        await SaveFileAsync(response, wallpaperPath);

        Log("Setting wallpaper");
        if (!WallpaperChanger.SetWallpaper(wallpaperPath))
        {
            Console.Error.WriteLine("Failed to set wallpaper");
            return 1;
        }
        Log("Done!");
        return 0;
    }

    private static string GetSelectedWallpaper()
    {
        if (_config.ImgURL is not null) return _config.ImgURL;
        if (_config.Shuffle)
            return _shuffleQueue.Count == 0 ? RefillAndReturn() : _shuffleQueue.Dequeue();
        return _wallpapers[_random.Next(_wallpapers.Length)];
    }

    private static string RefillAndReturn()
    {
        var items = _wallpapers.ToArray();
        for (int i = items.Length - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
        _shuffleQueue = new Queue<string>(items);
        return _shuffleQueue.Dequeue();
    }

    static readonly HashSet<string> AllowedTypes =
    [
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/bmp"
    ];

    private static async Task SaveFileAsync(HttpResponseMessage response, string path)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        long? totalBytes = response.Content.Headers.ContentLength;
        byte[] buffer = new byte[81920];
        long downloaded = 0;
        int read;
        int lastPercent = -1;

        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read));
            downloaded += read;

            if (_config.Verbose && totalBytes is not null)
            {
                int percent = (int)(downloaded * 100 / totalBytes.Value);
                if (percent != lastPercent)
                {
                    Console.Write($"\rDownloading: {percent}% ({Utils.FormatSize(downloaded)} / {Utils.FormatSize(totalBytes.Value)})");
                    lastPercent = percent;
                }
            }
        }
        if (_config.Verbose && totalBytes is not null) Console.WriteLine();
        await fileStream.FlushAsync();
    }

    static async Task<HttpResponseMessage> GetRetry(string url, int retries = 5)
    {
        for (int attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                Log($"Download attempt {attempt}/{retries}");
                var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                Log($"Server returned {(int)response.StatusCode}");
                if ((int)response.StatusCode >= 500 && attempt < retries)
                {
                    Log("Retrying...");
                    response.Dispose();
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                {
                    response.Dispose();
                    throw new HttpRequestException($"HTTP {(int)response.StatusCode}");
                }
                return response;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                Log($"Retrying after error: {ex.Message}");
                if (attempt == retries) throw;
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
            }
        }
        throw new Exception("Download failed");
    }
}
