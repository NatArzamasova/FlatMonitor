using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlatMonitor.Migrations
{
    /// <inheritdoc />
    public partial class AddScrapingHistorySources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sources",
                table: "ScrapingHistories",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sources",
                table: "ScrapingHistories");
        }
    }
}
