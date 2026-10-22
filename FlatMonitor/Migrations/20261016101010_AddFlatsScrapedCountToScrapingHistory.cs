using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlatMonitor.Migrations
{
    /// <inheritdoc />
    public partial class AddFlatsScrapedCountToScrapingHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FlatsScrapedCount",
                table: "ScrapingHistories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlatsScrapedCount",
                table: "ScrapingHistories");
        }
    }
}
