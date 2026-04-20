using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemConfigRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstructorPreferences");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SystemConfigs",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SystemConfigs");

            migrationBuilder.CreateTable(
                name: "InstructorPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClassroomId = table.Column<int>(type: "integer", nullable: true),
                    InstructorId = table.Column<int>(type: "integer", nullable: false),
                    TermId = table.Column<int>(type: "integer", nullable: true),
                    BuildingName = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    MaxHours = table.Column<int>(type: "integer", nullable: true),
                    PreferenceLevel = table.Column<int>(type: "integer", nullable: false),
                    PreferenceType = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    TimeSlotEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TimeSlotStart = table.Column<TimeOnly>(type: "time without time zone", nullable: true)
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
    }
}
