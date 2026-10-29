using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlatMonitor.Migrations
{
    /// <summary>
    /// The model cleanup changes no columns: Title and CommissionPercent keep their columns (now filled from Flat
    /// instead of FlatDetails), ScrapingHistory.Status stays a string, and the three FlatsSnapshots dates stay TEXT.
    /// Only the dates' content changes: they were written as round-trip strings ("2026-10-29T08:07:48.8973376Z"), which
    /// Microsoft.Data.Sqlite would read as local time. Rewrite them in EF Core's DateTime format (UTC, no zone suffix).
    /// </summary>
    /// <inheritdoc />
    public partial class ModelCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE FlatsSnapshots SET
                    ScrapedAtUtc = strftime('%Y-%m-%d %H:%M:%f', ScrapedAtUtc),
                    CreatedAtUtc = strftime('%Y-%m-%d %H:%M:%f', CreatedAtUtc),
                    UpdatedAtUtc = strftime('%Y-%m-%d %H:%M:%f', UpdatedAtUtc);
                """);

            // A backup left by a partially failed scrape still holds the old date strings; RestoreFlatsSnapshotsAsync
            // would copy them back unconverted. Every scrape creates a fresh backup, so dropping it loses nothing current.
            migrationBuilder.Sql("DROP TABLE IF EXISTS FlatsSnapshots_Backup;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE FlatsSnapshots SET
                    ScrapedAtUtc = strftime('%Y-%m-%dT%H:%M:%fZ', ScrapedAtUtc),
                    CreatedAtUtc = strftime('%Y-%m-%dT%H:%M:%fZ', CreatedAtUtc),
                    UpdatedAtUtc = strftime('%Y-%m-%dT%H:%M:%fZ', UpdatedAtUtc);
                """);
        }
    }
}
