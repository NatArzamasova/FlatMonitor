namespace workInProgress;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using workInProgress.Api.Services;
using workInProgress.Data;
using workInProgress.Scraping;
using workInProgress.Scraping.ImmobilienScout24;
using workInProgress.Scraping.OhneMakler;
using workInProgress.Storage;

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
