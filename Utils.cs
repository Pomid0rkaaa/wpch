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
}