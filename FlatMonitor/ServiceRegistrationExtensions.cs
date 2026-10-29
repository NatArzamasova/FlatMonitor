namespace FlatMonitor;

using FlatMonitor.Api;
using FlatMonitor.Api.Services;
using FlatMonitor.Scraping;
using FlatMonitor.Scraping.ImmobilienScout24;
using FlatMonitor.Scraping.OhneMakler;
using FlatMonitor.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class ServiceRegistrationExtensions
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";

    /// <summary>
    /// Services shared by the web server and the CLI: storage, scrapers, and scrape execution.
    /// </summary>
    public static IServiceCollection AddFlatMonitorCore(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        var storage = StorageOptions.FromConfiguration(configuration, contentRootPath);
        services.AddSingleton(storage);
        services.AddDbContext<FlatsDbContext>(options => options.UseSqlite(storage.ConnectionString));

        // All repositories share the scoped FlatsDbContext, so one scrape runs on one context and connection
        services.AddScoped<ISnapshotRepository, FlatSnapshotRepository>();
        services.AddScoped<IFlatAggregationRepository, FlatAggregationRepository>();
        services.AddScoped<IScrapingHistoryRepository, ScrapingHistoryRepository>();
        services.AddScoped<IFilterPresetRepository, FilterPresetRepository>();
        services.AddScoped<SnapshotWriter>();

        services.AddHttpClient<OhneMaklerScraper>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        });
        services.AddScoped<ImmobilienScout24Scraper>();

        // Sources are scraped in registration order
        services.AddScoped<IScraper>(sp => sp.GetRequiredService<OhneMaklerScraper>());
        services.AddScoped<IScraper>(sp => sp.GetRequiredService<ImmobilienScout24Scraper>());

        services.AddScoped(sp => new ScraperCoordinator(
            sp.GetServices<IScraper>(),
            sp.GetRequiredService<ISnapshotRepository>(),
            sp.GetRequiredService<SnapshotWriter>(),
            storage.DataDirectory,
            sp.GetRequiredService<ILogger<ScraperCoordinator>>()));
        services.AddScoped<ScrapeExecutionService>();

        return services;
    }

    /// <summary>
    /// Web server only, on top of <see cref="AddFlatMonitorCore"/>: the scrape job queue and its runner, the controllers,
    /// and <see cref="ApiExceptionHandler"/> (used by <c>app.UseExceptionHandler()</c>).
    /// </summary>
    public static IServiceCollection AddFlatMonitorApi(this IServiceCollection services)
    {
        services.AddSingleton<ScrapeJobService>();
        services.AddHostedService<ScrapeBackgroundService>();

        services.AddExceptionHandler<ApiExceptionHandler>();
        // Fallback for anything the handler does not write; UseExceptionHandler() also requires it to be configured
        services.AddProblemDetails();

        services.AddControllers();
        return services;
    }
}
