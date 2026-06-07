using System.Xml.Linq;

namespace wpch;

class Arguments
{
    public bool Verbose { get; set; }
    public bool ShowTitle { get; set; }
    public bool DryRun { get; set; }
    public bool CountOnly { get; set; }
    public bool Shuffle { get; set; }
    public int? Seed { get; set; }
    public string? ListPath { get; set; }
    public string[]? Include { get; set; }
    public string[]? Exclude { get; set; }
    public string? ImgURL { get; set; }
    public TimeSpan? Interval { get; set; }

    public static Arguments? Parse(string[] args)
    {
        var parsed = new Arguments();
        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--title":
                    case "-t":
                        parsed.ShowTitle = true;
                        break;
                    case "--verbose":
                    case "-v":
                        parsed.Verbose = true;
                        break;
                    case "--dry-run":
                    case "-d":
                        parsed.DryRun = true;
                        break;
                    case "--count":
                    case "-c":
                        parsed.CountOnly = true;
                        break;
                    case "--shuffle":
                    case "-s":
                        parsed.Shuffle = true;
                        break;
                    case "--help":
                    case "-h":
                        PrintHelp();
                        Environment.Exit(0);
                        break;
                    case "--version":
                    case "-V":
                        Console.WriteLine("wpch v1.3");
                        Environment.Exit(0);
                        break;
                    case "--seed":
                    case "-S":
                        if (parsed.Seed is not null) throw new ArgumentException("Seed specified more than once.");
                        if (!int.TryParse(RequireValue(args, ref i, "seed"), out int seed)) throw new ArgumentException("Invalid seed.");
                        parsed.Seed = seed;
                        break;
                    case "--img":
                    case "-I":
                        if (parsed.ImgURL is not null) throw new ArgumentException("Image specified more than once.");
                        if (i + 1 >= args.Length) throw new ArgumentException($"Missing value for image");
                        var img = args[++i];
                        if (img.StartsWith('-') && img != "-") throw new ArgumentException("Missing value for image");
                        parsed.ImgURL = img == "-" ? "stdin" : img;
                        break;
                    case "--interval":
                    case "-i":
                        if (parsed.Interval is not null) throw new ArgumentException("Interval specified more than once.");
                        parsed.Interval = ParseTime(RequireValue(args, ref i, "interval"));
                        break;
                    case "--list":
                    case "-l":
                        if (parsed.ListPath is not null) throw new ArgumentException("List specified more than once.");
                        if (i + 1 >= args.Length) throw new ArgumentException($"Missing value for list");
                        var ls = args[++i];
                        if (ls.StartsWith('-') && ls != "-") throw new ArgumentException("Missing value for list");
                        parsed.ListPath = ls == "-" ? "stdin" : ls;
                        break;
                    case "-f":
                        if (parsed.Include is not null && parsed.Exclude is not null) throw new ArgumentException("Filter specified more than once.");
                        if (i + 1 >= args.Length) throw new ArgumentException($"Missing value for list");
                        var filter = args[++i].Split(',');
                        List<string> include = [], exclude = [];
                        foreach (var f in filter)
                        {
                            if (f.StartsWith('-'))
                                exclude.Add(f[1..]);
                            else
                                include.Add(f);
                        }
                        parsed.Include = [.. include];
                        parsed.Exclude = [.. exclude];
                        break;
                    case "--include":
                        if (parsed.Include is not null) throw new ArgumentException("Include specified more than once.");
                        parsed.Include = RequireValue(args, ref i, "include").Split(',');
                        break;
                    case "--exclude":
                        if (parsed.Exclude is not null) throw new ArgumentException("Exclude specified more than once.");
                        parsed.Exclude = RequireValue(args, ref i, "exclude").Split(',');
                        break;
                    default:
                        Console.Error.WriteLine($"Unknown argument: {args[i]}");
                        return null;
                }
            }
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return null;
        }

        return parsed;
    }

    private static string RequireValue(string[] args, ref int i, string name)
    {
        if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
            throw new ArgumentException($"Missing value for {name}");
        return args[++i];
    }

    private static TimeSpan ParseTime(string input)
    {
        if (input.Length < 2) throw new ArgumentException("Invalid interval format");
        char unit = input[^1];
        if (!int.TryParse(input[..^1], out int value) || value <= 0)
            throw new ArgumentException("Invalid interval value");

        return unit switch
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
  -s, --shuffle          Cycle through wallpapers without repeats
  -S, --seed <n>         Use deterministic random seed
  -d, --dry-run          Show which wallpaper would be selected without downloading
  -I, --img <url>        Set wallpaper from a specific URL
  -V, --version          Print program version
""");
    }
}
