using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(string[]))]
internal partial class WallpaperJsonContext : JsonSerializerContext { }

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

    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    const int SW_HIDE = 0;

    static async Task<int> Main(string[] args)
    {
        string jsonPath = Path.Combine(AppContext.BaseDirectory, "wallpapers.json");
        if (!File.Exists(jsonPath))
        {
            Console.Error.WriteLine("wallpapers.json was not found");
            return 1;
        }

        try
        {
            string json = await File.ReadAllTextAsync(jsonPath);
            wallpapers = JsonSerializer.Deserialize(json, WallpaperJsonContext.Default.StringArray) ?? Array.Empty<string>();

            if (wallpapers.Length == 0)
            {
                Console.Error.WriteLine("wallpapers.json is empty or invalid");
                return 1;
            }
        }
        catch (JsonException)
        {
            Console.Error.WriteLine("wallpapers.json contains invalid JSON");
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
        catch (JsonException)
        {
            Console.Error.WriteLine("wallpapers.json contains invalid JSON");
            return 1;
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

        string fileName = Path.GetFileName(new Uri(selected).LocalPath);
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
        using var stream = await Http.GetStreamAsync(selected);
        using var fileStream = File.Create(wallpaperPath);
        await stream.CopyToAsync(fileStream);
        fileStream.Close();

        if (!WallpaperChanger.SetWallpaper(wallpaperPath))
        {
            Console.Error.WriteLine("Failed to set wallpaper");
            return 1;
        }

        Console.WriteLine($"Wallpaper set: {selected}");
        fileStream.Dispose();
        GC.Collect();
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

        return unit switch
        {
            's' => TimeSpan.FromSeconds(value),
            'm' => TimeSpan.FromMinutes(value),
            'h' => TimeSpan.FromHours(value),
            _ => throw new ArgumentException("Invalid interval unit, use s/m/h")
        };
    }
}