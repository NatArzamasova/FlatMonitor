using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace workInProgress.Migrations
{
    /// <inheritdoc />
    public partial class AddCommissionPercentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CommissionPercent",
                table: "FlatsSnapshots",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommissionPercent",
                table: "FlatsSnapshots");
        }
    }
}
