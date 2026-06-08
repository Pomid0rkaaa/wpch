using System.Net.Sockets;

namespace wpch;

public static class Utils
{
    public static string FormatSize(long bytes)
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

    public static async Task<(string Url, int Code, bool Ok)> CheckWallpaperAsync(string url)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, url);
            var res = await Program.Http.SendAsync(req);

            return (url, (int)res.StatusCode, res.IsSuccessStatusCode);
        }
        catch (HttpRequestException e)
        {
            int code = 0;

            if (e.InnerException is SocketException se)
                code = se.ErrorCode;

            return (url, code, false);
        }
    }
}