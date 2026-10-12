namespace workInProgress.Data;

using Microsoft.EntityFrameworkCore;
using workInProgress.Models;

public class ListingsDbContext : DbContext
{
    public DbSet<Listing> Listings { get; set; }

    public ListingsDbContext(DbContextOptions<ListingsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var listingBuilder = modelBuilder.Entity<Listing>();

        listingBuilder.HasKey(l => l.ListingId);
        listingBuilder.ToTable("Listings");

        listingBuilder.Property(l => l.ListingId)
            .HasMaxLength(50);

        listingBuilder.Property(l => l.Url)
            .IsRequired()
            .HasMaxLength(500);

        listingBuilder.Property(l => l.Title)
            .HasMaxLength(500);

        listingBuilder.Property(l => l.Address)
            .HasMaxLength(500);

        listingBuilder.Property(l => l.PostalCode)
            .HasMaxLength(10);

        listingBuilder.Property(l => l.District)
            .HasMaxLength(100);

        listingBuilder.Property(l => l.ScrapedAtUtc)
            .IsRequired()
            .HasMaxLength(50);

        // Owned type for Details — flattened into the Listings table
        listingBuilder.OwnsOne(l => l.Details, detailsBuilder =>
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

            detailsBuilder.Property(d => d.Flooring)
                .HasColumnName("Flooring")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.ServiceChargeEur)
                .HasColumnName("ServiceChargeEur");

            detailsBuilder.Property(d => d.EnergyCertificateType)
                .HasColumnName("EnergyCertificateType")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.PrimaryEnergySource)
                .HasColumnName("PrimaryEnergySource")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.ContactName)
                .HasColumnName("ContactName")
                .HasMaxLength(200);

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
        listingBuilder.HasIndex(l => l.ScrapedAtUtc)
            .HasDatabaseName("idx_ScrapedAtUtc");

        listingBuilder.HasIndex(l => l.PostalCode)
            .HasDatabaseName("idx_PostalCode");

        listingBuilder.HasIndex(l => l.District)
            .HasDatabaseName("idx_District");

        // Audit columns
        listingBuilder.Property<string>("CreatedAtUtc")
            .IsRequired()
            .HasMaxLength(50);

        listingBuilder.Property<string>("UpdatedAtUtc")
            .IsRequired()
            .HasMaxLength(50);
    }
}
