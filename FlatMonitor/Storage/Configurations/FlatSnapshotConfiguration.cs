namespace FlatMonitor.Storage.Configurations;

using FlatMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>The FlatsSnapshots table: one row per flat of the latest scrape, its details flattened into the same row.</summary>
internal sealed class FlatSnapshotConfiguration : IEntityTypeConfiguration<FlatSnapshot>
{
    public void Configure(EntityTypeBuilder<FlatSnapshot> builder)
    {

        builder.HasKey(s => new { s.Source, s.ListingId });
        builder.ToTable("FlatsSnapshots");

        builder.Property(s => s.Source)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.ListingId)
            .HasMaxLength(50);

        builder.Property(s => s.Url)
            .HasMaxLength(500);

        builder.Property(s => s.Address)
            .HasMaxLength(500);

        builder.Property(s => s.PostalCode)
            .HasMaxLength(10);

        builder.Property(s => s.District)
            .HasMaxLength(100);

        builder.Property(s => s.OriginalPriceEur);

        builder.Property(s => s.PricePerMeter);

        builder.Property(s => s.Title)
            .HasMaxLength(500);

        builder.Property(s => s.CommissionPercent);

        builder.Property(s => s.ScrapedAtUtc)
            .IsRequired()
            .HasConversion(UtcDateTimeConverter.Instance);

        // Owned type for Details, flattened into the FlatsSnapshots table
        builder.OwnsOne(s => s.Details, detailsBuilder =>
        {
            detailsBuilder.Property(d => d.PropertyType)
                .HasColumnName("PropertyType")
                .HasMaxLength(50);

            detailsBuilder.Property(d => d.BuildYear)
                .HasColumnName("BuildYear");

            detailsBuilder.Property(d => d.Floor)
                .HasColumnName("Floor")
                .HasMaxLength(50);

            detailsBuilder.Property(d => d.Condition)
                .HasColumnName("Condition")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.Heating)
                .HasColumnName("Heating")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.ServiceChargeEur)
                .HasColumnName("ServiceChargeEur");

            detailsBuilder.Property(d => d.EnergyCertificateType)
                .HasColumnName("EnergyCertificateType")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.PrimaryEnergySource)
                .HasColumnName("PrimaryEnergySource")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.EnergyEfficiencyClass)
                .HasColumnName("EnergyEfficiencyClass")
                .HasMaxLength(10);

            detailsBuilder.Property(d => d.EnergyConsumption)
                .HasColumnName("EnergyConsumption")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.ListingStatus)
                .HasColumnName("ListingStatus")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.CurrentUse)
                .HasColumnName("CurrentUse")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.Availability)
                .HasColumnName("Availability")
                .HasMaxLength(100);
        });

        // Indexes
        builder.HasIndex(s => new { s.Source, s.Url, s.Address })
            .IsUnique()
            .HasDatabaseName("idx_Source_Url_Address");

        builder.HasIndex(s => s.ScrapedAtUtc)
            .HasDatabaseName("idx_ScrapedAtUtc");

        builder.HasIndex(s => s.PostalCode)
            .HasDatabaseName("idx_PostalCode");

        builder.HasIndex(s => s.District)
            .HasDatabaseName("idx_District");

        // Audit columns
        builder.Property<DateTime>("CreatedAtUtc")
            .IsRequired()
            .HasConversion(UtcDateTimeConverter.Instance);

        builder.Property<DateTime>("UpdatedAtUtc")
            .IsRequired()
            .HasConversion(UtcDateTimeConverter.Instance);
    }
}
