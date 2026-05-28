using System.Runtime.InteropServices;
using System.Text.Json;

static class WallpaperChanger
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SystemParametersInfo(int uAction, int uParam, string lvpParam, int fuWinIni);

    private const int SPI_SETDESKWALLPAPER = 0x0014;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDWININICHANGE = 0x02;

    public static void SetWallpaper(string path) =>
        SystemParametersInfo(
            SPI_SETDESKWALLPAPER,
            0,
            path,
            SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE
        );
}

record Wallpaper(string title, string link);

class Program
{
    static async Task Main(string[] args)
    {
        Random rnd = new();

        string jsonPath = Path.Combine(AppContext.BaseDirectory, "wallpapers.json");
        string json = await File.ReadAllTextAsync(jsonPath);

        var wallpapers = JsonSerializer.Deserialize<List<Wallpaper>>(json);
        if (wallpapers == null || wallpapers.Count == 0)
            return;

        var selected = wallpapers[rnd.Next(wallpapers.Count)];
        Console.WriteLine($"Downloading: {selected.title}");

        using HttpClient client = new();
        byte[] fileBytes = await client.GetByteArrayAsync(selected.link);
        string wallpaperPath = Path.Combine(AppContext.BaseDirectory, "wallpaper.jpg");
        await File.WriteAllBytesAsync(wallpaperPath, fileBytes);

        WallpaperChanger.SetWallpaper(wallpaperPath);
        Console.WriteLine("Wallpaper changed!");
    }
}