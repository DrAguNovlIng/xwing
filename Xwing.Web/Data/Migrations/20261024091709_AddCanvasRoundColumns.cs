#pragma warning disable CA1861
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xwing.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCanvasRoundColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TemplateCanvasRounds",
                columns: table => new
                {
                    TemplateCanvasRoundId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentTemplateId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    WidthCells = table.Column<int>(type: "int", nullable: false),
                    Scenario = table.Column<int>(type: "int", nullable: false),
                    TimeCreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TimeUpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateCanvasRounds", x => x.TemplateCanvasRoundId);
                    table.ForeignKey(
                        name: "FK_TemplateCanvasRounds_TournamentTemplates_TournamentTemplateId",
                        column: x => x.TournamentTemplateId,
                        principalTable: "TournamentTemplates",
                        principalColumn: "TournamentTemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TournamentCanvasRounds",
                columns: table => new
                {
                    TournamentCanvasRoundId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    WidthCells = table.Column<int>(type: "int", nullable: false),
                    Scenario = table.Column<int>(type: "int", nullable: false),
                    TimeCreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TimeUpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentCanvasRounds", x => x.TournamentCanvasRoundId);
                    table.ForeignKey(
                        name: "FK_TournamentCanvasRounds_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "TournamentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateCanvasRounds_TournamentTemplateId_SortOrder",
                table: "TemplateCanvasRounds",
                columns: new[] { "TournamentTemplateId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentCanvasRounds_TournamentId_SortOrder",
                table: "TournamentCanvasRounds",
                columns: new[] { "TournamentId", "SortOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TemplateCanvasRounds");

            migrationBuilder.DropTable(
                name: "TournamentCanvasRounds");
        }
    }
}
