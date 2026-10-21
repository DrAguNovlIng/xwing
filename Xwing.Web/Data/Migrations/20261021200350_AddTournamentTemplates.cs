#pragma warning disable CA1861

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xwing.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TournamentTemplates",
                columns: table => new
                {
                    TournamentTemplateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TimeCreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TimeUpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentTemplates", x => x.TournamentTemplateId);
                });

            migrationBuilder.CreateTable(
                name: "TemplateMatches",
                columns: table => new
                {
                    TemplateMatchId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentTemplateId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PositionX = table.Column<int>(type: "int", nullable: false),
                    PositionY = table.Column<int>(type: "int", nullable: false),
                    TimeCreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TimeUpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateMatches", x => x.TemplateMatchId);
                    table.ForeignKey(
                        name: "FK_TemplateMatches_TournamentTemplates_TournamentTemplateId",
                        column: x => x.TournamentTemplateId,
                        principalTable: "TournamentTemplates",
                        principalColumn: "TournamentTemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateMatchFeeds",
                columns: table => new
                {
                    TemplateMatchFeedId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TargetTemplateMatchId = table.Column<int>(type: "int", nullable: false),
                    TargetSlot = table.Column<int>(type: "int", nullable: false),
                    SourceTemplateMatchId = table.Column<int>(type: "int", nullable: false),
                    SourceOutcome = table.Column<int>(type: "int", nullable: false),
                    TimeCreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TimeUpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateMatchFeeds", x => x.TemplateMatchFeedId);
                    table.ForeignKey(
                        name: "FK_TemplateMatchFeeds_TemplateMatches_SourceTemplateMatchId",
                        column: x => x.SourceTemplateMatchId,
                        principalTable: "TemplateMatches",
                        principalColumn: "TemplateMatchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TemplateMatchFeeds_TemplateMatches_TargetTemplateMatchId",
                        column: x => x.TargetTemplateMatchId,
                        principalTable: "TemplateMatches",
                        principalColumn: "TemplateMatchId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateMatches_TournamentTemplateId",
                table: "TemplateMatches",
                column: "TournamentTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateMatchFeeds_SourceTemplateMatchId_SourceOutcome",
                table: "TemplateMatchFeeds",
                columns: new[] { "SourceTemplateMatchId", "SourceOutcome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateMatchFeeds_TargetTemplateMatchId_TargetSlot",
                table: "TemplateMatchFeeds",
                columns: new[] { "TargetTemplateMatchId", "TargetSlot" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TemplateMatchFeeds");

            migrationBuilder.DropTable(
                name: "TemplateMatches");

            migrationBuilder.DropTable(
                name: "TournamentTemplates");
        }
    }
}
