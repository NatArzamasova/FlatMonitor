using Microsoft.Extensions.DependencyInjection;
using workInProgress.Scraping;
using workInProgress.Scraping.OhneMakler;
using workInProgress.Storage;

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

    var services = new ServiceCollection();

    services.AddSingleton<HttpClient>(sp =>
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        return client;
    });

    services.AddScoped<OhneMaklerScraper>();
    services.AddScoped<IScraper>(sp => sp.GetRequiredService<OhneMaklerScraper>());

    services.AddScoped<ISnapshotRepository, FlatSnapshotRepository>();
    services.AddScoped<SnapshotWriter>();

    services.AddScoped<ScraperCoordinator>(sp =>
    {
        var scrapers = new[] { sp.GetRequiredService<IScraper>() };
        var repository = sp.GetRequiredService<ISnapshotRepository>();
        var snapshotWriter = sp.GetRequiredService<SnapshotWriter>();
        return new ScraperCoordinator(scrapers, repository, snapshotWriter);
    });

    var serviceProvider = services.BuildServiceProvider();

    try
    {
        await using var coordinator = serviceProvider.GetRequiredService<ScraperCoordinator>();
        await coordinator.ScrapeAllSourcesAsync();
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Scrape failed: {ex.Message}");
        return 1;
    }
    finally
    {
        await serviceProvider.DisposeAsync();
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
