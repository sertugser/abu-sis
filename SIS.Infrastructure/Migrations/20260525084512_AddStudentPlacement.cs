using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentPlacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentPlacements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    EntranceType = table.Column<int>(type: "integer", nullable: true),
                    ScoreType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Score = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    RankOrder = table.Column<int>(type: "integer", nullable: true),
                    QuotaType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PlacementYear = table.Column<int>(type: "integer", nullable: true),
                    PreferenceOrder = table.Column<int>(type: "integer", nullable: true),
                    PlacedProgramName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPlacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentPlacements_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentPlacements_StudentId",
                table: "StudentPlacements",
                column: "StudentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentPlacements");
        }
    }
}
