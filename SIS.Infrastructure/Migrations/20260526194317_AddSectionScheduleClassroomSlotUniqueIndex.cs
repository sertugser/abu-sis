using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSectionScheduleClassroomSlotUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SectionSchedules_ClassroomId",
                table: "SectionSchedules");

            migrationBuilder.CreateIndex(
                name: "IX_SectionSchedules_Classroom_TimeSlot_Unique",
                table: "SectionSchedules",
                columns: new[] { "ClassroomId", "TimeSlotId" },
                unique: true,
                filter: "\"ClassroomId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SectionSchedules_Classroom_TimeSlot_Unique",
                table: "SectionSchedules");

            migrationBuilder.CreateIndex(
                name: "IX_SectionSchedules_ClassroomId",
                table: "SectionSchedules",
                column: "ClassroomId");
        }
    }
}
