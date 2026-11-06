namespace FlatMonitor.Storage.Configurations;

using FlatMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>The FlatPreviews table: one title photo per flat, keyed like FlatsSnapshots; the bytes as a BLOB.</summary>
internal sealed class FlatPreviewConfiguration : FlatPreviewRecordConfiguration<FlatPreview>
{
    public FlatPreviewConfiguration() : base("FlatPreviews")
    {
    }
}

/// <summary>
/// The FlatPreviewsArchive table: the first title photo of every flat ever seen, same columns and key as FlatPreviews.
/// Linked to FlatsArchive by (Source, ListingId) only, without a foreign key: one photo for all of a flat's rows.
/// </summary>
internal sealed class ArchivedFlatPreviewConfiguration : FlatPreviewRecordConfiguration<ArchivedFlatPreview>
{
    public ArchivedFlatPreviewConfiguration() : base("FlatPreviewsArchive")
    {
    }
}

/// <summary>The columns and key of a previews table.</summary>
internal abstract class FlatPreviewRecordConfiguration<T> : IEntityTypeConfiguration<T> where T : FlatPreviewRecord
{
    private readonly string _table;

    protected FlatPreviewRecordConfiguration(string table) => _table = table;

    public void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(p => new { p.Source, p.ListingId });
        builder.ToTable(_table);

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
