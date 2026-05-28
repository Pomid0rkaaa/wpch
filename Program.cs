using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(string[]))]
internal partial class WallpaperJsonContext : JsonSerializerContext {}

static class WallpaperChanger
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SystemParametersInfo(
        int uAction,
        int uParam,
        string lvpParam,
        int fuWinIni
    );

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

    static async Task<int> Main()
    {
        try 
        {
            string jsonPath = Path.Combine(AppContext.BaseDirectory, "wallpapers.json");
            if (!File.Exists(jsonPath))
            {
                Console.Error.WriteLine("wallpapers.json was not found");
                return 1;
            }

            string json = await File.ReadAllTextAsync(jsonPath);
            string[]? wallpapers = JsonSerializer.Deserialize(json, WallpaperJsonContext.Default.StringArray);
            if (wallpapers is not { Length: > 0 })
            {
                Console.Error.WriteLine("wallpapers.json is empty or invalid");
                return 1;
            }

            string selected = wallpapers[Random.Shared.Next(wallpapers.Length)];
            byte[] fileBytes;
            try
            {
                fileBytes = await Http.GetByteArrayAsync(selected);
            }
            catch (HttpRequestException)
            {
                Console.Error.WriteLine("Failed to download wallpaper. Check your internet connection or URL");
                return 1;
            }
            string wallpaperPath = Path.Combine(AppContext.BaseDirectory, "wallpaper.jpg");
            await File.WriteAllBytesAsync(wallpaperPath, fileBytes);
            
            if (!WallpaperChanger.SetWallpaper(wallpaperPath))
            {
                Console.Error.WriteLine("Failed to set wallpaper");
                return 1;
            }
            return 0;
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
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            return 1;
        }
    }
}