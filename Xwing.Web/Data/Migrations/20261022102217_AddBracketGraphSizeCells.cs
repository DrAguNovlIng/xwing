using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xwing.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBracketGraphSizeCells : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GraphHeightCells",
                table: "TournamentTemplates",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "GraphWidthCells",
                table: "TournamentTemplates",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "GraphHeightCells",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "GraphWidthCells",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 30);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GraphHeightCells",
                table: "TournamentTemplates");

            migrationBuilder.DropColumn(
                name: "GraphWidthCells",
                table: "TournamentTemplates");

            migrationBuilder.DropColumn(
                name: "GraphHeightCells",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "GraphWidthCells",
                table: "Tournaments");
        }
    }
}
