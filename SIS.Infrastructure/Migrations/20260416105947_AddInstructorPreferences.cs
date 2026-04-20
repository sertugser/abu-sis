using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInstructorPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstructorPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstructorId = table.Column<int>(type: "integer", nullable: false),
                    PreferenceType = table.Column<int>(type: "integer", nullable: false),
                    PreferenceLevel = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    TimeSlotStart = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TimeSlotEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ClassroomId = table.Column<int>(type: "integer", nullable: true),
                    BuildingName = table.Column<string>(type: "text", nullable: true),
                    MaxHours = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    TermId = table.Column<int>(type: "integer", nullable: true),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstructorPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstructorPreferences_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InstructorPreferences_Classrooms_ClassroomId",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InstructorPreferences_Instructors_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Instructors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstructorPreferences_ClassroomId",
                table: "InstructorPreferences",
                column: "ClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorPreferences_InstructorId",
                table: "InstructorPreferences",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorPreferences_InstructorId_TermId",
                table: "InstructorPreferences",
                columns: new[] { "InstructorId", "TermId" });

            migrationBuilder.CreateIndex(
                name: "IX_InstructorPreferences_TermId",
                table: "InstructorPreferences",
                column: "TermId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstructorPreferences");
        }
    }
}
