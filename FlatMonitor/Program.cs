using dotenv.net;
using FlatMonitor;
using FlatMonitor.Api.Services;
using FlatMonitor.Models;
using FlatMonitor.Scraping;
using FlatMonitor.Scraping.ImmobilienScout24;
using FlatMonitor.Scraping.Immowelt;
using FlatMonitor.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var commandArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();

try
{
    // .env lives in the repository root; probe upwards from the build output (bin/<config>/<tfm>)
    DotEnv.Load(new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 5));

    switch (CliCommandParser.Parse(commandArgs))
    {
        case CliCommand.Help:
            PrintUsage();
            return 0;
        case CliCommand.Scrape:
            string? sources;
            try
            {
                sources = CliCommandParser.ParseSources(commandArgs);
                ScraperSources.Parse(sources);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                Console.Error.WriteLine(ex.Message);
                PrintUsage();
                return 1;
            }
            return await RunScrapeAsync(sources);
        case CliCommand.ExploreImmobilienScout24:
            return await ExploreImmobilienScout24Async();
        case CliCommand.ExploreImmowelt:
            return await ExploreImmoweltAsync(
                headless: commandArgs.Contains("--headless"),
                cdpUrl: commandArgs.Contains("--cdp") ? ImmoweltOptions.DefaultCdpUrl : null);
        case CliCommand.Unknown:
            Console.Error.WriteLine($"Unknown command: {commandArgs[0]}");
            PrintUsage();
            return 1;
        default:
            return await RunWebServerAsync(commandArgs);
    }
}
catch (Exception ex)
{
    // No host (and so no logger) exists here: print the whole exception
    Console.Error.WriteLine($"Fatal error: {ex}");
    return 1;
}

async Task<int> RunScrapeAsync(string? sources)
{
    Console.WriteLine(sources == null ? "Starting scraper..." : $"Starting scraper for {sources}...");

    // --source overrides Scrapers:Sources of appsettings.json and the environment
    using var host = BuildCliHost(sources == null ? null : new() { [ScraperSources.ConfigurationKey] = sources });
    await using var scope = host.Services.CreateAsyncScope();

    try
    {
        await MigrateDatabaseAsync(scope.ServiceProvider);

        // Same run as a job of the web server: recorded in ScrapingHistories, logged step by step; a source is
        // only published once it has finished, so a failed or canceled run leaves FlatsSnapshots consistent
        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelOnCtrlC = (_, e) =>
        {
            // A second Ctrl+C ends the process at once
            if (cts.IsCancellationRequested)
                return;
            e.Cancel = true;
            Console.Error.WriteLine("Canceling the scrape... (press Ctrl+C again to quit immediately)");
            cts.Cancel();
        };
        Console.CancelKeyPress += cancelOnCtrlC;

        ScrapeRunResult result;
        try
        {
            result = await scope.ServiceProvider.GetRequiredService<ScrapeRunService>().RunAsync(cts.Token);
        }
        finally
        {
            Console.CancelKeyPress -= cancelOnCtrlC;
        }

        switch (result.Status)
        {
            case ScrapingStatus.Successful:
                Console.WriteLine($"✓ Scraping completed. Total flats scraped: {result.Summary!.TotalFlats}");
                return 0;
            case ScrapingStatus.PartialFailed:
                Console.Error.WriteLine("⚠ Scraping completed with errors:");
                foreach (var error in result.Summary!.Errors)
                {
                    Console.Error.WriteLine($"  - {error}");
                }
                Console.WriteLine($"✓ Partially scraped: {result.Summary.TotalFlats} flats collected (with errors)");
                return 1;
            case ScrapingStatus.Canceled:
                Console.Error.WriteLine("✗ Scraping canceled");
                return 1;
            default:
                Console.Error.WriteLine($"✗ Scraping failed: {result.ErrorMessage}");
                return 1;
        }
    }
    catch (Exception ex)
    {
        host.Services.GetRequiredService<ILogger<Program>>().LogError(ex, "Scrape failed");
        return 1;
    }
}

async Task<int> ExploreImmobilienScout24Async()
{
    Console.WriteLine("ImmobilienScout24 Explorer with Playwright");
    Console.WriteLine("==========================================\n");

    using var host = BuildCliHost();
    var browser = new ImmobilienScout24Browser(
        host.Services.GetRequiredService<ImmobilienScout24Options>(),
        host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("FlatMonitor.ExploreImmobilienScout24"));

    try
    {
        await using var session = await browser.LaunchAsync();

        var html = await session.Page.ContentAsync();

        Console.WriteLine("\n✓ Page fetched successfully!");
        Console.WriteLine($"Page size: {html.Length} bytes");

        // Save the HTML for analysis
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            $"immobilienscout24_listings_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html");

        await File.WriteAllTextAsync(outputPath, html);
        Console.WriteLine($"\n✓ HTML saved to: {outputPath}");
        Console.WriteLine("You can now analyze the page structure by opening this file.");

        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
}

async Task<int> ExploreImmoweltAsync(bool headless, string? cdpUrl)
{
    using var host = BuildCliHost();
    var storage = host.Services.GetRequiredService<StorageOptions>();
    var explorer = new ImmoweltExplorer(
        Path.Combine(storage.DataDirectory, "explore", "Immowelt"),
        headless,
        cdpUrl,
        host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("FlatMonitor.ExploreImmowelt"));

    try
    {
        return await explorer.ExploreAsync() ? 0 : 1;
    }
    catch (Exception ex)
    {
        host.Services.GetRequiredService<ILogger<Program>>().LogError(ex, "Immowelt exploration failed");
        return 1;
    }
}

// The services of the CLI commands: configuration, logging and AddFlatMonitorCore, as the web server has them.
// No args: the command name is not configuration. Command options that are configuration come as overrides.
IHost BuildCliHost(Dictionary<string, string?>? overrides = null)
{
    var builder = Host.CreateApplicationBuilder();
    if (overrides != null)
        builder.Configuration.AddInMemoryCollection(overrides);
    builder.Logging.AddFlatMonitorLogging();
    builder.Services.AddFlatMonitorCore(builder.Configuration, builder.Environment.ContentRootPath);
    return builder.Build();
}

async Task<int> RunWebServerAsync(string[] args)
{
    try
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Logging.AddFlatMonitorLogging();

        builder.Services.AddFlatMonitorCore(builder.Configuration, builder.Environment.ContentRootPath);

        builder.Services.AddFlatMonitorApi();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // First, so it also covers exceptions from later middleware; writes an ApiResponse 500 (ApiExceptionHandler)
        app.UseExceptionHandler();

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

        app.Lifetime.ApplicationStarted.Register(() =>
            app.Logger.LogInformation("API server started on {Urls}", string.Join(", ", app.Urls)));
        await app.RunAsync();
        return 0;
    }
    catch (Exception ex)
    {
        // The host may not exist yet (failed while building): print the whole exception
        Console.Error.WriteLine($"Web server failed: {ex}");
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
    Console.Error.WriteLine("Default: Starts the web server (http://localhost:5000 unless --urls or ASPNETCORE_URLS set another)");
    Console.Error.WriteLine("Commands:");
    Console.Error.WriteLine("  scrape [--source <names>]");
    Console.Error.WriteLine("                Fetch current listings into the database (CLI mode, then exit);");
    Console.Error.WriteLine($"                --source scrapes only these, comma-separated: {string.Join(", ", ScraperSources.All)}");
    Console.Error.WriteLine("                (the other sources keep their flats)");
    Console.Error.WriteLine("  explore-is24  Open the ImmobilienScout24 listings page and save its HTML for analysis");
    Console.Error.WriteLine("  explore-immowelt [--headless | --cdp]");
    Console.Error.WriteLine("                Open the Immowelt search, save two listings pages and an expose for analysis;");
    Console.Error.WriteLine($"                --cdp attaches to a Chrome started with --remote-debugging-port ({ImmoweltOptions.DefaultCdpUrl})");
    Console.Error.WriteLine("  --help        Show this message");
}
