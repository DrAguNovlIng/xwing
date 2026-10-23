using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xwing.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFormatDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FormatDescription",
                table: "TournamentTemplates",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormatDescription",
                table: "Tournaments",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormatDescription",
                table: "TournamentTemplates");

            migrationBuilder.DropColumn(
                name: "FormatDescription",
                table: "Tournaments");
        }
    }
}
