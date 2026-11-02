namespace FlatMonitor;

/// <summary>What <c>dotnet run [-- &lt;command&gt;]</c> runs.</summary>
public enum CliCommand
{
    WebServer,
    Scrape,
    ExploreImmobilienScout24,
    ExploreImmowelt,
    Help,

    /// <summary>A word that is no command, e.g. a typo like "scrap": must not silently start the web server.</summary>
    Unknown,
}

public static class CliCommandParser
{
    /// <summary>
    /// The command named by the first argument. No arguments, or options only (e.g. <c>--urls</c>, which the web
    /// server reads as configuration), start the web server.
    /// </summary>
    public static CliCommand Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
            return CliCommand.WebServer;

        return args[0] switch
        {
            "--help" or "-h" => CliCommand.Help,
            "scrape" => CliCommand.Scrape,
            "explore-is24" => CliCommand.ExploreImmobilienScout24,
            "explore-immowelt" => CliCommand.ExploreImmowelt,
            var option when option.StartsWith('-') => CliCommand.WebServer,
            _ => CliCommand.Unknown,
        };
    }

    public const string SourceOption = "--source";

    /// <summary>
    /// The value of <c>--source</c> (e.g. <c>Immowelt</c> or <c>OhneMakler,Immowelt</c>), or <c>null</c> without the
    /// option. Throws if the option has no value.
    /// </summary>
    public static string? ParseSources(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] != SourceOption)
                continue;

            return i + 1 < args.Count && !args[i + 1].StartsWith('-')
                ? args[i + 1]
                : throw new ArgumentException($"{SourceOption} needs a source name, e.g. {SourceOption} Immowelt");
        }

        return null;
    }
}
