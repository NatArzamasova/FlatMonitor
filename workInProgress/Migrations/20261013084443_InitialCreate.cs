using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace workInProgress.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OhneMaklerSnapshots",
                columns: table => new
                {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OhneMaklerSnapshots");
        }
    }
}
