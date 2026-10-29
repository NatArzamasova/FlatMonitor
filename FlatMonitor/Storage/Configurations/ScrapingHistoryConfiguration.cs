namespace FlatMonitor.Storage.Configurations;

using FlatMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>The ScrapingHistories table: one row per scrape run.</summary>
internal sealed class ScrapingHistoryConfiguration : IEntityTypeConfiguration<ScrapingHistory>
{
    public void Configure(EntityTypeBuilder<ScrapingHistory> builder)
    {
        builder.HasKey(h => h.Id);
        builder.ToTable("ScrapingHistories");
        builder.Property(h => h.StartTimeUtc).IsRequired();
        builder.Property(h => h.EndTimeUtc);
        // Stored by name, as before the enum existed
        builder.Property(h => h.Status).IsRequired().HasMaxLength(50).HasConversion<string>();
        builder.Property(h => h.ErrorMessage).HasMaxLength(500);
        builder.Property(h => h.FlatsScrapedCount).IsRequired().HasDefaultValue(0);
    }
}
