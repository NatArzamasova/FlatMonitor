using dotenv.net;
using FlatMonitor;
using FlatMonitor.Api.Services;
using FlatMonitor.Scraping.ImmobilienScout24;
using FlatMonitor.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var commandArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();

try
{
    // .env lives in the repository root; probe upwards from the build output (bin/<config>/<tfm>)
    DotEnv.Load(new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 5));

    if (commandArgs.Length > 0 && (commandArgs[0] == "--help" || commandArgs[0] == "-h"))
    {
        PrintUsage();
        return 0;
    }

    // If scrape or explore-is24 command, run in CLI mode
    if (commandArgs.Length > 0)
    {
        return commandArgs[0] switch
        {
            "scrape" => await RunScrapeAsync(),
            "explore-is24" => await ExploreImmobilienScout24Async(),
            _ => await RunWebServerAsync(commandArgs)
        };
    }

    // Default: run web server
    return await RunWebServerAsync(commandArgs);
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

    // No args: "scrape" is a command, not configuration
    var builder = Host.CreateApplicationBuilder();
    builder.Logging.AddFlatMonitorLogging();
    builder.Services.AddFlatMonitorCore(builder.Configuration, builder.Environment.ContentRootPath);
    using var host = builder.Build();
    await using var scope = host.Services.CreateAsyncScope();

    try
    {
        await MigrateDatabaseAsync(scope.ServiceProvider);

        var snapshotRepo = scope.ServiceProvider.GetRequiredService<ISnapshotRepository>();
        var executionService = scope.ServiceProvider.GetRequiredService<ScrapeExecutionService>();

        try
        {
            // ScrapeExecutionService logs each step (backup, clear, refresh, delete backup) when it happens
            var result = await executionService.ExecuteAsync();

            if (result.Success)
            {
                Console.WriteLine($"✓ Scraping completed. Total flats scraped: {result.FlatsCount}");
                return 0;
            }
            else
            {
                Console.Error.WriteLine("⚠ Scraping completed with errors:");
                foreach (var error in result.ScraperErrors)
                {
                    Console.Error.WriteLine($"  - {error}");
                }
                Console.WriteLine($"✓ Partially scraped: {result.FlatsCount} flats collected (with errors)");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Scrape failed, restoring from backup: {ex.Message}");
            try
            {
                await snapshotRepo.RestoreFlatsSnapshotsAsync();
                Console.WriteLine("✓ Data restored from backup");
            }
            catch (Exception restoreEx)
            {
                Console.Error.WriteLine($"Failed to restore backup: {restoreEx.Message}");
            }
            throw;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Scrape failed: {ex.Message}");
        return 1;
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

async Task<int> RunWebServerAsync(string[] args)
{
    try
    {
        Console.WriteLine("Starting FlatMonitor Web Server...");
        var builder = WebApplication.CreateBuilder(args);

        builder.Logging.AddFlatMonitorLogging();

        builder.Services.AddFlatMonitorCore(builder.Configuration, builder.Environment.ContentRootPath);

        builder.Services.AddSingleton<ScrapeStateService>();
        builder.Services.AddSingleton<ScrapeJobService>();
        builder.Services.AddSingleton<ScrapeBackgroundService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ScrapeBackgroundService>());

        builder.Services.AddControllers();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            await MigrateDatabaseAsync(scope.ServiceProvider);
        }

        if (app.Environment.EnvironmentName == "Development")
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        Console.WriteLine("🚀 API server started on http://localhost:5000");
        await app.RunAsync();
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Web server failed: {ex.Message}");
        if (Environment.GetEnvironmentVariable("DEBUG") == "1")
            Console.Error.WriteLine(ex.StackTrace);
        return 1;
    }
}

async Task MigrateDatabaseAsync(IServiceProvider services)
{
    services.GetRequiredService<StorageOptions>().EnsureDirectoriesExist();
    await services.GetRequiredService<FlatsDbContext>().Database.MigrateAsync();
}

void PrintUsage()
{
    Console.Error.WriteLine("Usage: dotnet run [-- <command>]");
    Console.Error.WriteLine("Default: Starts the web server on http://localhost:5000");
    Console.Error.WriteLine("Commands:");
    Console.Error.WriteLine("  scrape        Fetch current listings and write snapshot (CLI mode, then exit)");
    Console.Error.WriteLine("  explore-is24  Login and fetch ImmobilienScout24 listings page for analysis");
    Console.Error.WriteLine("  --help        Show this message");
}
