using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlatMonitor.Migrations
{
    /// <summary>
    /// A scrape now writes into FlatsSnapshots_Staging and replaces one source at a time in FlatsSnapshots, instead of
    /// backing up, clearing and refilling FlatsSnapshots. The backup table is no longer used; a leftover is dropped.
    /// </summary>
    /// <inheritdoc />
    public partial class AddFlatsSnapshotsStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlatsSnapshots_Staging",
                columns: table => new
                {
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ListingId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_FlatsSnapshots_Staging", x => new { x.Source, x.ListingId });
                });

            migrationBuilder.CreateIndex(
                name: "idx_Staging_Source_Url_Address",
                table: "FlatsSnapshots_Staging",
                columns: new[] { "Source", "Url", "Address" },
                unique: true);

            migrationBuilder.Sql("DROP TABLE IF EXISTS FlatsSnapshots_Backup;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlatsSnapshots_Staging");
        }
    }
}
