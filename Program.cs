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

    public static void SetWallpaper(string path) =>
        SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
}

class Program
{
    static async Task Main()
    {
        string jsonPath = Path.Combine(AppContext.BaseDirectory, "wallpapers.json");
        string json = await File.ReadAllTextAsync(jsonPath);

        string[]? wallpapers = JsonSerializer.Deserialize(json, WallpaperJsonContext.Default.StringArray);
        if (wallpapers is not { Length: > 0 }) return;
        string selected = wallpapers[Random.Shared.Next(wallpapers.Length)];

        using HttpClient client = new();
        byte[] fileBytes = await client.GetByteArrayAsync(selected);

        string wallpaperPath = Path.Combine(AppContext.BaseDirectory, "wallpaper.jpg");
        await File.WriteAllBytesAsync(wallpaperPath, fileBytes);
        
        WallpaperChanger.SetWallpaper(wallpaperPath);
    }
}