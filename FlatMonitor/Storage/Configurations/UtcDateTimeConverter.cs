namespace FlatMonitor.Storage.Configurations;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>SQLite has no time zone: values are written as UTC and read back with <see cref="DateTimeKind.Utc"/>.</summary>
internal static class UtcDateTimeConverter
{
    public static readonly ValueConverter<DateTime, DateTime> Instance =
        new(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
