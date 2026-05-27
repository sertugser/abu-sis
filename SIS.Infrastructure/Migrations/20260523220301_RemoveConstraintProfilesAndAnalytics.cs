using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveConstraintProfilesAndAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConstraintProfileItems");

            migrationBuilder.DropTable(
                name: "ConstraintViolationSnapshotDetails");

            migrationBuilder.DropTable(
                name: "ConstraintProfiles");

            migrationBuilder.DropTable(
                name: "ConstraintViolationSnapshots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConstraintProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstraintProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConstraintViolationSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    IsFeasible = table.Column<bool>(type: "boolean", nullable: false),
                    SnapshotAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SnapshotType = table.Column<int>(type: "integer", nullable: false),
                    TotalHardViolations = table.Column<int>(type: "integer", nullable: false),
                    TotalSoftPenalty = table.Column<double>(type: "double precision", nullable: false)
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
                name: "ConstraintProfileItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConstraintDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    ProfileId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Parameters = table.Column<string>(type: "text", nullable: true),
                    Weight = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstraintProfileItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstraintProfileItems_ConstraintDefinitions_ConstraintDefi~",
                        column: x => x.ConstraintDefinitionId,
                        principalTable: "ConstraintDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConstraintProfileItems_ConstraintProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "ConstraintProfiles",
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
                    AffectedSectionCount = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    ConstraintCode = table.Column<string>(type: "text", nullable: false),
                    Penalty = table.Column<double>(type: "double precision", nullable: false),
                    ViolationCount = table.Column<int>(type: "integer", nullable: false),
                    WeightedPenalty = table.Column<double>(type: "double precision", nullable: false)
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
                name: "IX_ConstraintProfileItems_ConstraintDefinitionId",
                table: "ConstraintProfileItems",
                column: "ConstraintDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstraintProfileItems_ProfileId_ConstraintDefinitionId",
                table: "ConstraintProfileItems",
                columns: new[] { "ProfileId", "ConstraintDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConstraintViolationSnapshotDetails_SnapshotId",
                table: "ConstraintViolationSnapshotDetails",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstraintViolationSnapshots_TermId_SnapshotAt",
                table: "ConstraintViolationSnapshots",
                columns: new[] { "TermId", "SnapshotAt" });
        }
    }
}
