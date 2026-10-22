using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlatMonitor.Migrations
{
    /// <inheritdoc />
    public partial class AddFlatAggregationsWithScrapingDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlatAggregations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    District = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RoomsCount = table.Column<double>(type: "REAL", nullable: true),
                    Count = table.Column<int>(type: "INTEGER", nullable: false),
                    AvgPricePerMeter = table.Column<decimal>(type: "TEXT", nullable: false),
                    ScrapingDate = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlatAggregations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_FlatAggregations_District_RoomsCount",
                table: "FlatAggregations",
                columns: new[] { "District", "RoomsCount" });

            migrationBuilder.CreateIndex(
                name: "idx_FlatAggregations_ScrapingDate",
                table: "FlatAggregations",
                column: "ScrapingDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlatAggregations");
        }
    }
}
