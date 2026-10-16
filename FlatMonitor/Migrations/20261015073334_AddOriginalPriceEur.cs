using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace workInProgress.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginalPriceEur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginalPriceEur",
                table: "FlatsSnapshots",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalPriceEur",
                table: "FlatsSnapshots");
        }
    }
}
