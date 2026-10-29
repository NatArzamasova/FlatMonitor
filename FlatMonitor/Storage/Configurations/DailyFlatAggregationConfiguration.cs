namespace FlatMonitor.Storage.Configurations;

using FlatMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>The FlatAggregations table: price per m² by district and rooms, per scraping date.</summary>
internal sealed class FlatAggregationSnapshotConfiguration : IEntityTypeConfiguration<FlatAggregationSnapshot>
{
    public void Configure(EntityTypeBuilder<FlatAggregationSnapshot> builder)
    {
        builder.HasKey(a => a.Id);
        builder.ToTable("FlatAggregations");

        builder.Property(a => a.District)
            .HasMaxLength(100);

        builder.Property(a => a.RoomsCount);

        builder.Property(a => a.Count)
            .IsRequired();

        builder.Property(a => a.AvgPricePerMeter)
            .IsRequired()
            .HasColumnType("TEXT");

        builder.Property(a => a.ScrapingDate)
            .IsRequired();

        builder.HasIndex(a => new { a.District, a.RoomsCount })
            .HasDatabaseName("idx_FlatAggregations_District_RoomsCount");

        builder.HasIndex(a => a.ScrapingDate)
            .HasDatabaseName("idx_FlatAggregations_ScrapingDate");
    }
}
