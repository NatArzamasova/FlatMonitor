namespace FlatMonitor.Storage.Configurations;

using FlatMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>The FlatsSnapshots table: one row per flat of the latest published scrape of its source.</summary>
internal sealed class PublishedFlatConfiguration : FlatRecordConfiguration<PublishedFlat>
{
    public PublishedFlatConfiguration() : base("FlatsSnapshots", indexPrefix: "idx_")
    {
    }

    protected override void ConfigureIndexes(EntityTypeBuilder<PublishedFlat> builder)
    {
        builder.HasIndex(s => s.ScrapedAtUtc)
            .HasDatabaseName("idx_ScrapedAtUtc");

        builder.HasIndex(s => s.PostalCode)
            .HasDatabaseName("idx_PostalCode");

        builder.HasIndex(s => s.District)
            .HasDatabaseName("idx_District");
    }
}

/// <summary>
/// The FlatsSnapshots_Staging table: the running scrape, written batch by batch and published per source.
/// Same columns and unique index as FlatsSnapshots, so a staged source always fits into it; no read indexes.
/// </summary>
internal sealed class StagedFlatConfiguration : FlatRecordConfiguration<StagedFlat>
{
    public StagedFlatConfiguration() : base("FlatsSnapshots_Staging", indexPrefix: "idx_Staging_")
    {
    }
}

/// <summary>The columns of a flats table, its details flattened into the same row.</summary>
internal abstract class FlatRecordConfiguration<T> : IEntityTypeConfiguration<T> where T : FlatRecord
{
    private readonly string _table;
    private readonly string _indexPrefix;

    protected FlatRecordConfiguration(string table, string indexPrefix)
    {
        _table = table;
        _indexPrefix = indexPrefix;
    }

    /// <summary>Indexes beyond the key and the unique index; SQLite index names are per database.</summary>
    protected virtual void ConfigureIndexes(EntityTypeBuilder<T> builder)
    {
    }

    public void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(s => new { s.Source, s.ListingId });
        builder.ToTable(_table);

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
            .HasDatabaseName($"{_indexPrefix}Source_Url_Address");

        ConfigureIndexes(builder);

        // Audit columns
        builder.Property<DateTime>("CreatedAtUtc")
            .IsRequired()
            .HasConversion(UtcDateTimeConverter.Instance);

        builder.Property<DateTime>("UpdatedAtUtc")
            .IsRequired()
            .HasConversion(UtcDateTimeConverter.Instance);
    }
}
