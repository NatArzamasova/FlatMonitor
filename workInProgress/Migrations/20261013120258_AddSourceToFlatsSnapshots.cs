using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace workInProgress.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceToFlatsSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OhneMaklerSnapshots");

            migrationBuilder.CreateTable(
                name: "FlatsSnapshots",
                columns: table => new
                {
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ListingId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    District = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PriceEur = table.Column<int>(type: "INTEGER", nullable: true),
                    RoomsCount = table.Column<int>(type: "INTEGER", nullable: true),
                    LivingAreaSqm = table.Column<int>(type: "INTEGER", nullable: true),
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
                    ScrapedAtUtc = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UpdatedAtUtc = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlatsSnapshots", x => new { x.Source, x.ListingId });
                });

            migrationBuilder.CreateIndex(
                name: "idx_District",
                table: "FlatsSnapshots",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "idx_PostalCode",
                table: "FlatsSnapshots",
                column: "PostalCode");

            migrationBuilder.CreateIndex(
                name: "idx_ScrapedAtUtc",
                table: "FlatsSnapshots",
                column: "ScrapedAtUtc");

            migrationBuilder.CreateIndex(
                name: "idx_Source_Url_Address",
                table: "FlatsSnapshots",
                columns: new[] { "Source", "Url", "Address" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlatsSnapshots");

            migrationBuilder.CreateTable(
                name: "OhneMaklerSnapshots",
                columns: table => new
                {
                    ListingId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    District = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LivingAreaSqm = table.Column<int>(type: "INTEGER", nullable: true),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    PriceEur = table.Column<int>(type: "INTEGER", nullable: true),
                    RoomsCount = table.Column<int>(type: "INTEGER", nullable: true),
                    ScrapedAtUtc = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UpdatedAtUtc = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Availability = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BuildYear = table.Column<int>(type: "INTEGER", nullable: true),
                    Condition = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CurrentUse = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EnergyCertificateType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EnergyConsumption = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EnergyEfficiencyClass = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Floor = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Heating = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ListingStatus = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PrimaryEnergySource = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PropertyType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ServiceChargeEur = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OhneMaklerSnapshots", x => x.ListingId);
                });

            migrationBuilder.CreateIndex(
                name: "idx_District",
                table: "OhneMaklerSnapshots",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "idx_PostalCode",
                table: "OhneMaklerSnapshots",
                column: "PostalCode");

            migrationBuilder.CreateIndex(
                name: "idx_ScrapedAtUtc",
                table: "OhneMaklerSnapshots",
                column: "ScrapedAtUtc");
        }
    }
}
