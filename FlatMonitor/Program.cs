using dotenv.net;
using FlatMonitor;
using FlatMonitor.Api.Services;
using FlatMonitor.Scraping.ImmobilienScout24;
using FlatMonitor.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
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
            return await RunScrapeAsync();
        case CliCommand.ExploreImmobilienScout24:
            return await ExploreImmobilienScout24Async();
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

        var executionService = scope.ServiceProvider.GetRequiredService<ScrapeExecutionService>();

        // ScrapeExecutionService logs each step when it happens; a source is only published once it has
        // finished, so a failed run leaves FlatsSnapshots consistent
        var result = await executionService.ExecuteAsync();

        if (result.Success)
        {
            Console.WriteLine($"✓ Scraping completed. Total flats scraped: {result.TotalFlats}");
            return 0;
        }

        Console.Error.WriteLine("⚠ Scraping completed with errors:");
        foreach (var error in result.Errors)
        {
            Console.Error.WriteLine($"  - {error}");
        }
        Console.WriteLine($"✓ Partially scraped: {result.TotalFlats} flats collected (with errors)");
        return 1;
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

    using var loggerFactory = LoggerFactory.Create(logging => logging.AddFlatMonitorLogging());
    var options = ImmobilienScout24Options.FromConfiguration(Host.CreateApplicationBuilder().Configuration);
    var scraper = new ImmobilienScout24BrowserScraper(options, loggerFactory.CreateLogger("FlatMonitor.ExploreImmobilienScout24"));

    try
    {
        await using var session = await scraper.LaunchAndAuthenticateAsync();

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
    Console.Error.WriteLine("Default: Starts the web server on http://localhost:5000");
    Console.Error.WriteLine("Commands:");
    Console.Error.WriteLine("  scrape        Fetch current listings and write snapshot (CLI mode, then exit)");
    Console.Error.WriteLine("  explore-is24  Login and fetch ImmobilienScout24 listings page for analysis");
    Console.Error.WriteLine("  --help        Show this message");
}
