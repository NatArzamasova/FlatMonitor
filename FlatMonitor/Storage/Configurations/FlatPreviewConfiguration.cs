namespace FlatMonitor.Storage.Configurations;

using FlatMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>The FlatPreviews table: one title photo per flat, keyed like FlatsSnapshots; the bytes as a BLOB.</summary>
internal sealed class FlatPreviewConfiguration : IEntityTypeConfiguration<FlatPreview>
{
    public void Configure(EntityTypeBuilder<FlatPreview> builder)
    {
        builder.HasKey(p => new { p.Source, p.ListingId });
        builder.ToTable("FlatPreviews");

        builder.Property(p => p.Source)
            .HasMaxLength(50);

        builder.Property(p => p.ListingId)
            .HasMaxLength(50);

        builder.Property(p => p.ImageUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(p => p.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Data)
            .IsRequired();

        builder.Property(p => p.DownloadedAtUtc)
            .IsRequired()
            .HasConversion(UtcDateTimeConverter.Instance);
    }
}
