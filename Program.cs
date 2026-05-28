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

    private static string[] wallpapers = Array.Empty<string>();

    static async Task<int> Main(string[] args)
    {
        string listPath = Path.Combine(AppContext.BaseDirectory, "wallpapers.txt");
        if (!File.Exists(listPath))
        {
            Console.Error.WriteLine("wallpapers.txt was not found");
            return 1;
        }

        wallpapers = (await File.ReadAllLinesAsync(listPath))
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !x.StartsWith('#'))
            .ToArray();
        if (wallpapers.Length == 0)
        {
            Console.Error.WriteLine("wallpapers.txt is empty");
            return 1;
        }

        TimeSpan? interval = null;
        try
        {
            interval = ParseInterval(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Invalid interval argument: {ex.Message}");
            return 1;
        }

        if (interval is null)
        {
            return await RunOnceErrors();
        }

        var next = DateTime.UtcNow + interval.Value;
        while (true)
        {
            await RunOnceErrors();

            next += interval.Value;
            var delay = next - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay);
            else
                next = DateTime.UtcNow;
        }
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
        string selected = wallpapers[Random.Shared.Next(wallpapers.Length)];

        if (!Uri.TryCreate(selected, UriKind.Absolute, out var uri))
        {
            Console.Error.WriteLine($"Invalid URL: {selected}");
            return 1;
        }

        string fileName = Path.GetFileName(uri.LocalPath);
        string extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".bmp")
        {
            Console.Error.WriteLine($"Unsupported wallpaper format: {extension}");
            return 1;
        }

        using var response = await Http.GetAsync(selected, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is not ("image/jpeg" or "image/png" or "image/bmp"))
        {
            Console.Error.WriteLine($"Unsupported wallpaper MIME type: {contentType}");
            return 1;
        }

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

        Console.WriteLine($"Wallpaper set: {selected}");
        return 0;
    }

    static TimeSpan? ParseInterval(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--interval" && i + 1 < args.Length)
            {
                return ParseTime(args[i + 1]);
            }
        }
        return null;
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