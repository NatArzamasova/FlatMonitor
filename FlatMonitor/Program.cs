using dotenv.net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;
using workInProgress.Data;
using workInProgress.Scraping;
using workInProgress.Scraping.ImmobilienScout24;
using workInProgress.Scraping.OhneMakler;
using workInProgress.Storage;

var commandArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();

try
{
    // Change to solution root to find .env
    var solutionRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
    solutionRoot = Path.GetFullPath(solutionRoot);
    Console.WriteLine($"AppContext.BaseDirectory: {AppContext.BaseDirectory}");
    Console.WriteLine($"Solution root: {solutionRoot}");
    var envFile = Path.Combine(solutionRoot, ".env");
    Console.WriteLine($".env path: {envFile}");
    Console.WriteLine($".env exists: {File.Exists(envFile)}");

    Directory.SetCurrentDirectory(solutionRoot);
    DotEnv.Load();

    if (commandArgs.Length == 0 || commandArgs[0] == "--help" || commandArgs[0] == "-h")
    {
        PrintUsage();
        return 0;
    }

    return commandArgs[0] switch
    {
        "scrape" => await RunScrapeAsync(),
        "explore-is24" => await ExploreImmobilienScout24Async(),
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

    // Add DbContext
    var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "FlatMonitor", "data", "listings.db");
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    services.AddDbContext<FlatsDbContext>(options =>
        options.UseSqlite($"Data Source={dbPath}"));

    services.AddSingleton<HttpClient>(sp =>
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        return client;
    });

    services.AddScoped<OhneMaklerScraper>();
    services.AddScoped<ImmobilienScout24Scraper>();

    services.AddScoped<ISnapshotRepository>(sp => new FlatSnapshotRepository(sp.GetRequiredService<DbContextOptions<FlatsDbContext>>()));
    services.AddScoped<SnapshotWriter>();

    services.AddScoped<ScraperCoordinator>(sp =>
    {
        var scrapers = new IScraper[]
        {
            sp.GetRequiredService<OhneMaklerScraper>(),
            sp.GetRequiredService<ImmobilienScout24Scraper>(),
        };
        var repository = sp.GetRequiredService<ISnapshotRepository>();
        var snapshotWriter = sp.GetRequiredService<SnapshotWriter>();
        return new ScraperCoordinator(scrapers, repository, snapshotWriter);
    });

    var serviceProvider = services.BuildServiceProvider();

    try
    {
        // Apply migrations
        await using var dbContext = serviceProvider.GetRequiredService<FlatsDbContext>();
        await dbContext.Database.MigrateAsync();

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

async Task<int> ExploreImmobilienScout24Async()
{
    Console.WriteLine("ImmobilienScout24 Explorer with Playwright");
    Console.WriteLine("==========================================\n");

    var scraper = new ImmobilienScout24BrowserScraper();

    try
    {
        var (browser, page) = await scraper.LaunchAndAuthenticateAsync();

        if (browser == null || page == null)
        {
            Console.Error.WriteLine("Failed to launch browser or authenticate.");
            return 1;
        }

        var html = await page.ContentAsync();

        Console.WriteLine("\n✓ Page fetched successfully!");
        Console.WriteLine($"Page size: {html.Length} bytes");

        // Save the HTML for analysis
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            $"immobilienscout24_listings_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html");

        await File.WriteAllTextAsync(outputPath, html);
        Console.WriteLine($"\n✓ HTML saved to: {outputPath}");
        Console.WriteLine("You can now analyze the page structure by opening this file.");

        await scraper.DisposeAsync();
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
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
    Console.Error.WriteLine("  scrape        Fetch current listings and write snapshot");
    Console.Error.WriteLine("  explore-is24  Login and fetch ImmobilienScout24 listings page for analysis");
    Console.Error.WriteLine("  --help        Show this message");
}
