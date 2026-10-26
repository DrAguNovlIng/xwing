using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xwing.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentChampionAndFinale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FinaleTournamentMatchId",
                table: "Tournaments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WinnerUserId",
                table: "Tournaments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_FinaleTournamentMatchId",
                table: "Tournaments",
                column: "FinaleTournamentMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_WinnerUserId",
                table: "Tournaments",
                column: "WinnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tournaments_TournamentMatches_FinaleTournamentMatchId",
                table: "Tournaments",
                column: "FinaleTournamentMatchId",
                principalTable: "TournamentMatches",
                principalColumn: "TournamentMatchId",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Tournaments_Users_WinnerUserId",
                table: "Tournaments",
                column: "WinnerUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tournaments_TournamentMatches_FinaleTournamentMatchId",
                table: "Tournaments");

            migrationBuilder.DropForeignKey(
                name: "FK_Tournaments_Users_WinnerUserId",
                table: "Tournaments");

            migrationBuilder.DropIndex(
                name: "IX_Tournaments_FinaleTournamentMatchId",
                table: "Tournaments");

            migrationBuilder.DropIndex(
                name: "IX_Tournaments_WinnerUserId",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "FinaleTournamentMatchId",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "WinnerUserId",
                table: "Tournaments");
        }
    }
}
