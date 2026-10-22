#pragma warning disable CA1861
#pragma warning disable CA1823

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xwing.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentBracketAndInactiveUsers : Migration
    {
        private static readonly string[] SourceOutcomeIndexColumns = ["SourceTournamentMatchId", "SourceOutcome"];

        private static readonly string[] TargetSlotIndexColumns = ["TargetTournamentMatchId", "TargetSlot"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "TournamentTemplateId",
                table: "Tournaments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoundNumber",
                table: "TemplateMatches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TournamentMatches",
                columns: table => new
                {
                    TournamentMatchId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PositionX = table.Column<int>(type: "int", nullable: false),
                    PositionY = table.Column<int>(type: "int", nullable: false),
                    SlotAOpenLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SlotBOpenLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoundNumber = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PlayMode = table.Column<int>(type: "int", nullable: false),
                    ReplayUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SlotAUserId = table.Column<int>(type: "int", nullable: true),
                    SlotBUserId = table.Column<int>(type: "int", nullable: true),
                    SlotAPoints = table.Column<int>(type: "int", nullable: true),
                    SlotBPoints = table.Column<int>(type: "int", nullable: true),
                    Winner = table.Column<int>(type: "int", nullable: false),
                    TimeCreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TimeUpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentMatches", x => x.TournamentMatchId);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "TournamentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_Users_SlotAUserId",
                        column: x => x.SlotAUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_Users_SlotBUserId",
                        column: x => x.SlotBUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "TournamentMatchFeeds",
                columns: table => new
                {
                    TournamentMatchFeedId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TargetTournamentMatchId = table.Column<int>(type: "int", nullable: false),
                    TargetSlot = table.Column<int>(type: "int", nullable: false),
                    SourceTournamentMatchId = table.Column<int>(type: "int", nullable: false),
                    SourceOutcome = table.Column<int>(type: "int", nullable: false),
                    TimeCreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TimeUpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentMatchFeeds", x => x.TournamentMatchFeedId);
                    table.ForeignKey(
                        name: "FK_TournamentMatchFeeds_TournamentMatches_SourceTournamentMatchId",
                        column: x => x.SourceTournamentMatchId,
                        principalTable: "TournamentMatches",
                        principalColumn: "TournamentMatchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentMatchFeeds_TournamentMatches_TargetTournamentMatchId",
                        column: x => x.TargetTournamentMatchId,
                        principalTable: "TournamentMatches",
                        principalColumn: "TournamentMatchId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_TournamentTemplateId",
                table: "Tournaments",
                column: "TournamentTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_SlotAUserId",
                table: "TournamentMatches",
                column: "SlotAUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_SlotBUserId",
                table: "TournamentMatches",
                column: "SlotBUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_TournamentId",
                table: "TournamentMatches",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatchFeeds_SourceTournamentMatchId_SourceOutcome",
                table: "TournamentMatchFeeds",
                columns: SourceOutcomeIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatchFeeds_TargetTournamentMatchId_TargetSlot",
                table: "TournamentMatchFeeds",
                columns: TargetSlotIndexColumns,
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tournaments_TournamentTemplates_TournamentTemplateId",
                table: "Tournaments",
                column: "TournamentTemplateId",
                principalTable: "TournamentTemplates",
                principalColumn: "TournamentTemplateId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tournaments_TournamentTemplates_TournamentTemplateId",
                table: "Tournaments");

            migrationBuilder.DropTable(
                name: "TournamentMatchFeeds");

            migrationBuilder.DropTable(
                name: "TournamentMatches");

            migrationBuilder.DropIndex(
                name: "IX_Tournaments_TournamentTemplateId",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TournamentTemplateId",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "RoundNumber",
                table: "TemplateMatches");
        }
    }
}
