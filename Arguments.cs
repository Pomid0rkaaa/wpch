namespace wpch;

public class Arguments
{
    public bool Verbose { get; set; }
    public bool ShowTitle { get; set; }
    public bool DryRun { get; set; }
    public bool CountOnly { get; set; }
    public bool ListAll { get; set; }
    public bool Shuffle { get; set; }
    public int? Seed { get; set; }
    public string? ListPath { get; set; }
    public string[]? Include { get; set; }
    public string[]? Exclude { get; set; }
    public string? ImgURL { get; set; }
    public TimeSpan? Interval { get; set; }
}


public class ArgumentParser
{
    private record Option(Action<string?> Handler, bool TakesValue = false);

    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>()
    {
        ["-t"] = "--title",
        ["-v"] = "--verbose",
        ["-d"] = "--dry-run",
        ["-c"] = "--count",
        ["-L"] = "--list-all",
        ["-s"] = "--shuffle",
        ["-h"] = "--help",
        ["-V"] = "--version",
        ["-S"] = "--seed",
        ["-I"] = "--img",
        ["-i"] = "--interval",
        ["-l"] = "--list",
    };

    public static Arguments? Parse(string[] args)
    {
        var parsed = new Arguments();

        var map = new Dictionary<string, Option>
        {
            ["--title"] = new(_ => parsed.ShowTitle = true),
            ["--verbose"] = new(_ => parsed.Verbose = true),
            ["--dry-run"] = new(_ => parsed.DryRun = true),
            ["--count"] = new(_ => parsed.CountOnly = true),
            ["--list-all"] = new(_ => parsed.ListAll = true),
            ["--shuffle"] = new(_ => parsed.Shuffle = true),
            ["--help"] = new(_ =>
            {
                PrintHelp();
                Environment.Exit(0);
            }),
            ["--version"] = new(_ =>
            {
                Console.WriteLine("wpch v1.4");
                Environment.Exit(0);
            }),
            ["--seed"] = new(v =>
            {
                EnsureNotSet(parsed.Seed, "Seed");

                if (!int.TryParse(v, out var seed))
                    throw new ArgumentException("Invalid seed.");

                parsed.Seed = seed;
            }, true),
            ["--img"] = new(v =>
            {
                EnsureNotSet(parsed.ImgURL, "Image");
                parsed.ImgURL = v == "-" ? "stdin" : v;
            }, true),
            ["--interval"] = new(v =>
            {
                EnsureNotSet(parsed.Interval, "Interval");
                parsed.Interval = ParseTime(v!);
            }, true),
            ["--list"] = new(v =>
            {
                EnsureNotSet(parsed.ListPath, "List");
                if (v!.StartsWith('-') && v != "v")
                    throw new ArgumentException("Missing value for list");
                parsed.ListPath = v == "-" ? "stdin" : v;
            }, true),
            ["--include"] = new(v =>
            {
                EnsureNotSet(parsed.Include, "Include");
                parsed.Include = v!.Split(',', StringSplitOptions.RemoveEmptyEntries);
            }, true),
            ["--exclude"] = new(v =>
            {
                EnsureNotSet(parsed.Exclude, "Exclude");
                parsed.Exclude = v!.Split(',', StringSplitOptions.RemoveEmptyEntries);
            }, true),
            ["-f"] = new(v =>
            {
                if (parsed.Include is not null || parsed.Exclude is not null)
                    throw new ArgumentException("Cannot mix -f with --include/--exclude");
                var filter = v!.Split(',', StringSplitOptions.RemoveEmptyEntries);
                parsed.Include = [.. filter.Where(f => !f.StartsWith('-'))];
                parsed.Exclude = [.. filter.Where(f => f.StartsWith('-')).Select(f => f[1..])];
            }, true),
        };

        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                var key = args[i];

                if (Aliases.TryGetValue(key, out var normalized))
                    key = normalized;

                if (!map.TryGetValue(key, out var opt))
                    throw new ArgumentException($"Unknown argument: {key}");

                string? value = null;

                if (opt.TakesValue)
                    value = RequireValue(args, ref i, key);

                opt.Handler(value);
            }
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return null;
        }

        return parsed;
    }

    private static void EnsureNotSet<T>(T? val, string name)
    {
        if (val is not null) throw new ArgumentException($"{name} specified more than once.");
    }

    private static string RequireValue(string[] args, ref int i, string name)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"Missing value for {name}");
        return args[++i];
    }

    private static TimeSpan ParseTime(string input)
    {
        if (input.Length < 2) throw new ArgumentException("Invalid interval format");

        if (!int.TryParse(input[..^1], out int value) || value <= 0)
            throw new ArgumentException("Invalid interval value");

        return input[^1] switch
        {
            's' => TimeSpan.FromSeconds(value),
            'm' => TimeSpan.FromMinutes(value),
            'h' => TimeSpan.FromHours(value),
            _ => throw new ArgumentException("Invalid interval unit, use s/m/h")
        };
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
Usage: wpch [options]
Options:
  -l, --list <path>      Path to wallpapers list file
  -i, --interval <time>  Interval (e.g. 10s, 5m, 1h)
      --include <text>   Filter wallpapers including comma separated substring
      --exclude <text>   Filter wallpapers excluding comma separated substring
  -f <text>              Filter wallpapers by comma separated substrings
                           prefix '-' to exclude
  -v, --verbose          Show download and selection details
  -t, --title            Print selected wallpaper name
  -h, --help             Show help
  -c, --count            Show number of wallpapers matching filter
  -L, --list-all         List all wallpapers matching filter
  -s, --shuffle          Cycle through wallpapers without repeats
  -S, --seed <n>         Use deterministic random seed
  -d, --dry-run          Show which wallpaper would be selected without downloading
  -I, --img <url>        Set wallpaper from a specific URL
  -V, --version          Print program version
""");
    }
}
