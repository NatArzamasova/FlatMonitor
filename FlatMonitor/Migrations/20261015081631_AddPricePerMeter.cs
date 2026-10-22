using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlatMonitor.Migrations
{
    /// <inheritdoc />
    public partial class AddPricePerMeter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PricePerMeter",
                table: "FlatsSnapshots",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PricePerMeter",
                table: "FlatsSnapshots");
        }
    }
}
