using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConstraintSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConstraintViolationSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    SnapshotAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalHardViolations = table.Column<int>(type: "integer", nullable: false),
                    TotalSoftPenalty = table.Column<double>(type: "double precision", nullable: false),
                    IsFeasible = table.Column<bool>(type: "boolean", nullable: false),
                    SnapshotType = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstraintViolationSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstraintViolationSnapshots_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConstraintViolationSnapshotDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SnapshotId = table.Column<int>(type: "integer", nullable: false),
                    ConstraintCode = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    ViolationCount = table.Column<int>(type: "integer", nullable: false),
                    Penalty = table.Column<double>(type: "double precision", nullable: false),
                    WeightedPenalty = table.Column<double>(type: "double precision", nullable: false),
                    AffectedSectionCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstraintViolationSnapshotDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstraintViolationSnapshotDetails_ConstraintViolationSnaps~",
                        column: x => x.SnapshotId,
                        principalTable: "ConstraintViolationSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConstraintViolationSnapshotDetails_SnapshotId",
                table: "ConstraintViolationSnapshotDetails",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstraintViolationSnapshots_TermId_SnapshotAt",
                table: "ConstraintViolationSnapshots",
                columns: new[] { "TermId", "SnapshotAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConstraintViolationSnapshotDetails");

            migrationBuilder.DropTable(
                name: "ConstraintViolationSnapshots");
        }
    }
}
