namespace FlatMonitor;

using FlatMonitor.Api;
using FlatMonitor.Api.Services;
using FlatMonitor.Scraping;
using FlatMonitor.Scraping.ImmobilienScout24;
using FlatMonitor.Scraping.Immowelt;
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
        services.AddScoped<IFlatRepository, FlatRepository>();
        services.AddScoped<IFlatAggregationRepository, FlatAggregationRepository>();
        services.AddScoped<IScrapingHistoryRepository, ScrapingHistoryRepository>();
        services.AddScoped<IFilterPresetRepository, FilterPresetRepository>();
        services.AddScoped<IFlatPreviewRepository, FlatPreviewRepository>();
        services.AddScoped<JsonlBackupWriter>();

        services.AddHttpClient<OhneMaklerScraper>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        });
        services.AddHttpClient<FlatPreviewDownloader>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
            client.DefaultRequestHeaders.Add("Accept", "image/webp,image/jpeg,image/*");
        });
        services.AddScoped<IFlatPreviewDownloader>(sp => sp.GetRequiredService<FlatPreviewDownloader>());
        services.AddSingleton(ImmobilienScout24Options.FromConfiguration(configuration));
        services.AddScoped<ImmobilienScout24Scraper>();
        services.AddSingleton(ImmoweltOptions.FromConfiguration(configuration));
        services.AddScoped<ImmoweltScraper>();

        // Sources are scraped at the same time and reported in registration order (that of ScraperSources.All); only the selected ones
        var sources = ScraperSources.FromConfiguration(configuration);
        if (sources.Contains(OhneMaklerScraper.SourceName))
            services.AddScoped<IScraper>(sp => sp.GetRequiredService<OhneMaklerScraper>());
        if (sources.Contains(ImmobilienScout24Scraper.SourceName))
            services.AddScoped<IScraper>(sp => sp.GetRequiredService<ImmobilienScout24Scraper>());
        if (sources.Contains(ImmoweltPages.SourceName))
            services.AddScoped<IScraper>(sp => sp.GetRequiredService<ImmoweltScraper>());
        services.AddSingleton(new AvailableScraperSources(ScraperSources.All.Where(sources.Contains).ToList()));

        services.AddScoped<ScraperCoordinator>();
        services.AddScoped<ScrapeExecutionService>();
        services.AddScoped<ScrapeRunService>();

        return services;
    }

    /// <summary>
    /// Web server only, on top of <see cref="AddFlatMonitorCore"/>: the scrape job queue and its runner, the controllers,
    /// and <see cref="ApiExceptionHandler"/> (used by <c>app.UseExceptionHandler()</c>).
    /// </summary>
    public static IServiceCollection AddFlatMonitorApi(this IServiceCollection services)
    {
        services.AddSingleton<ScrapeJobService>();
        services.AddScoped<ScrapeStartupRecovery>();
        services.AddHostedService<ScrapeBackgroundService>();

        services.AddExceptionHandler<ApiExceptionHandler>();
        // Fallback for anything the handler does not write; UseExceptionHandler() also requires it to be configured
        services.AddProblemDetails();

        services.AddControllers();
        return services;
    }
}
