using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xwing.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentParticipantSquads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SquadFaction",
                table: "TournamentParticipants",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SquadShipsJson",
                table: "TournamentParticipants",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SquadUrl",
                table: "TournamentParticipants",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SquadFaction",
                table: "TournamentParticipants");

            migrationBuilder.DropColumn(
                name: "SquadShipsJson",
                table: "TournamentParticipants");

            migrationBuilder.DropColumn(
                name: "SquadUrl",
                table: "TournamentParticipants");
        }
    }
}
