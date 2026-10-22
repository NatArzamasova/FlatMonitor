namespace FlatMonitor;

using FlatMonitor.Api.Services;
using FlatMonitor.Scraping;
using FlatMonitor.Scraping.ImmobilienScout24;
using FlatMonitor.Scraping.OhneMakler;
using FlatMonitor.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddScrapeServices(this IServiceCollection services)
    {
        services.AddScoped<OhneMaklerScraper>();
        services.AddScoped<ImmobilienScout24Scraper>();
        services.AddScoped<ISnapshotRepository>(sp => new FlatSnapshotRepository(sp.GetRequiredService<DbContextOptions<FlatsDbContext>>()));
        services.AddScoped<IFlatAggregationRepository>(sp => new FlatAggregationRepository(sp.GetRequiredService<DbContextOptions<FlatsDbContext>>()));
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
        services.AddScoped<ScrapeExecutionService>();

        return services;
    }
}
