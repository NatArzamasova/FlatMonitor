using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlatMonitor.Migrations
{
    /// <inheritdoc />
    public partial class AddFlatsArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlatPreviewsArchive",
                columns: table => new
                {
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ListingId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    DownloadedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlatPreviewsArchive", x => new { x.Source, x.ListingId });
                });

            migrationBuilder.CreateTable(
                name: "FlatsArchive",
                columns: table => new
                {
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ListingId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ScrapingHistoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    District = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PriceEur = table.Column<int>(type: "INTEGER", nullable: true),
                    OriginalPriceEur = table.Column<int>(type: "INTEGER", nullable: true),
                    PricePerMeter = table.Column<int>(type: "INTEGER", nullable: true),
                    RoomsCount = table.Column<double>(type: "REAL", nullable: true),
                    LivingAreaSqm = table.Column<double>(type: "REAL", nullable: true),
                    CommissionPercent = table.Column<double>(type: "REAL", nullable: true),
                    PropertyType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BuildYear = table.Column<int>(type: "INTEGER", nullable: true),
                    Floor = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Condition = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Heating = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ServiceChargeEur = table.Column<int>(type: "INTEGER", nullable: true),
                    EnergyCertificateType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PrimaryEnergySource = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EnergyEfficiencyClass = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    EnergyConsumption = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ListingStatus = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CurrentUse = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Availability = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ScrapedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlatsArchive", x => new { x.Source, x.ListingId, x.ScrapingHistoryId });
                    table.ForeignKey(
                        name: "FK_FlatsArchive_ScrapingHistories_ScrapingHistoryId",
                        column: x => x.ScrapingHistoryId,
                        principalTable: "ScrapingHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_Archive_ScrapingHistoryId",
                table: "FlatsArchive",
                column: "ScrapingHistoryId");

            // Backfill: the archive starts with today's flats and photos, not empty. Each flat gets the run that
            // published it: the latest run started before it was scraped. Runs never overlap, and a canceled or failed
            // run may still have published sources, so the status does not matter. Dates are stored as text of the
            // same format, so they compare as strings. A flat older than every run (none in practice) is left out:
            // the run id is part of the key.
            migrationBuilder.Sql("""
                INSERT INTO FlatsArchive (
                    Source, ListingId, ScrapingHistoryId, CreatedAtUtc, UpdatedAtUtc, Url, Title, Address, PostalCode,
                    District, PriceEur, OriginalPriceEur, PricePerMeter, RoomsCount, LivingAreaSqm, CommissionPercent,
                    PropertyType, BuildYear, Floor, Condition, Heating, ServiceChargeEur, EnergyCertificateType,
                    PrimaryEnergySource, EnergyEfficiencyClass, EnergyConsumption, ListingStatus, CurrentUse,
                    Availability, ScrapedAtUtc)
                SELECT * FROM (
                    SELECT
                        s.Source, s.ListingId,
                        (SELECT h.Id FROM ScrapingHistories h
                         WHERE h.StartTimeUtc <= s.ScrapedAtUtc
                         ORDER BY h.StartTimeUtc DESC, h.Id DESC
                         LIMIT 1) AS RunId,
                        s.CreatedAtUtc, s.UpdatedAtUtc, s.Url, s.Title, s.Address, s.PostalCode,
                        s.District, s.PriceEur, s.OriginalPriceEur, s.PricePerMeter, s.RoomsCount, s.LivingAreaSqm,
                        s.CommissionPercent, s.PropertyType, s.BuildYear, s.Floor, s.Condition, s.Heating,
                        s.ServiceChargeEur, s.EnergyCertificateType, s.PrimaryEnergySource, s.EnergyEfficiencyClass,
                        s.EnergyConsumption, s.ListingStatus, s.CurrentUse, s.Availability, s.ScrapedAtUtc
                    FROM FlatsSnapshots s)
                WHERE RunId IS NOT NULL;

                INSERT INTO FlatPreviewsArchive (Source, ListingId, ImageUrl, ContentType, Data, DownloadedAtUtc)
                SELECT Source, ListingId, ImageUrl, ContentType, Data, DownloadedAtUtc FROM FlatPreviews;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlatPreviewsArchive");

            migrationBuilder.DropTable(
                name: "FlatsArchive");
        }
    }
}
