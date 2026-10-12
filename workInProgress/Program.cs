using workInProgress.Scraping;

var commandArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();

try
{
    if (commandArgs.Length == 0 || commandArgs[0] == "--help" || commandArgs[0] == "-h")
    {
        PrintUsage();
        return 0;
    }

    return commandArgs[0] switch
    {
        "scrape" => await RunScrapeAsync(),
        _ => PrintUsageAndFail($"Unknown command: {commandArgs[0]}")
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fatal error: {ex.Message}");
    if (Environment.GetEnvironmentVariable("DEBUG") == "1")
        Console.Error.WriteLine(ex.StackTrace);
    return 1;
}

async Task<int> RunScrapeAsync()
{
    Console.WriteLine("Starting scraper...");

    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

    var scraper = new ListingScraper(httpClient);

    try
    {
        await scraper.RunAsync();
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Scrape failed: {ex.Message}");
        return 1;
    }
}

int PrintUsageAndFail(string? message = null)
{
    if (message != null)
        Console.Error.WriteLine($"Error: {message}");
    PrintUsage();
    return 1;
}

void PrintUsage()
{
    Console.Error.WriteLine("Usage: dotnet run -- <command>");
    Console.Error.WriteLine("Commands:");
    Console.Error.WriteLine("  scrape    Fetch current listings and write snapshot");
    Console.Error.WriteLine("  --help    Show this message");
}
