namespace FlatMonitor.Storage.Configurations;

using FlatMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>The FilterPresets table: saved district filters, at most one of them the default.</summary>
internal sealed class FilterPresetConfiguration : IEntityTypeConfiguration<FilterPreset>
{
    public void Configure(EntityTypeBuilder<FilterPreset> builder)
    {
        builder.HasKey(p => p.Id);
        builder.ToTable("FilterPresets");

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(FilterPreset.MaxNameLength)
            .UseCollation("NOCASE");

        // Primitive collection, stored as a JSON text column
        builder.PrimitiveCollection(p => p.Districts)
            .IsRequired();

        builder.Property(p => p.IsDefault)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(p => p.Name)
            .IsUnique()
            .HasDatabaseName("idx_FilterPresets_Name");

        // Partial unique index: at most one preset can be the default
        builder.HasIndex(p => p.IsDefault)
            .IsUnique()
            .HasFilter("\"IsDefault\" = 1")
            .HasDatabaseName("idx_FilterPresets_SingleDefault");
    }
}
