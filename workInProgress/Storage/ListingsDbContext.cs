namespace workInProgress.Data;

using Microsoft.EntityFrameworkCore;
using workInProgress.Models;

public class ListingsDbContext : DbContext
{
    public DbSet<FlatSnapshot> FlatsSnapshots { get; set; }

    public ListingsDbContext(DbContextOptions<ListingsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var snapshotBuilder = modelBuilder.Entity<FlatSnapshot>();

        snapshotBuilder.HasKey(s => new { s.Source, s.ListingId });
        snapshotBuilder.ToTable("FlatsSnapshots");

        snapshotBuilder.Property(s => s.Source)
            .IsRequired()
            .HasMaxLength(50);

        snapshotBuilder.Property(s => s.ListingId)
            .HasMaxLength(50);

        snapshotBuilder.Property(s => s.Url)
            .HasMaxLength(500);

        snapshotBuilder.Property(s => s.Title)
            .HasMaxLength(500);

        snapshotBuilder.Property(s => s.Address)
            .HasMaxLength(500);

        snapshotBuilder.Property(s => s.PostalCode)
            .HasMaxLength(10);

        snapshotBuilder.Property(s => s.District)
            .HasMaxLength(100);

        snapshotBuilder.Property(s => s.ScrapedAtUtc)
            .IsRequired()
            .HasMaxLength(50);

        // Owned type for Details — flattened into the OhneMaklerSnapshots table
        snapshotBuilder.OwnsOne(s => s.Details, detailsBuilder =>
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
        snapshotBuilder.HasIndex(s => new { s.Source, s.Url, s.Address })
            .IsUnique()
            .HasDatabaseName("idx_Source_Url_Address");

        snapshotBuilder.HasIndex(s => s.ScrapedAtUtc)
            .HasDatabaseName("idx_ScrapedAtUtc");

        snapshotBuilder.HasIndex(s => s.PostalCode)
            .HasDatabaseName("idx_PostalCode");

        snapshotBuilder.HasIndex(s => s.District)
            .HasDatabaseName("idx_District");

        // Audit columns
        snapshotBuilder.Property<string>("CreatedAtUtc")
            .IsRequired()
            .HasMaxLength(50);

        snapshotBuilder.Property<string>("UpdatedAtUtc")
            .IsRequired()
            .HasMaxLength(50);
    }
}
