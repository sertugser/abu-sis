using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <summary>
    /// SectionSchedule unique index'ini ikiliden (CourseSectionId, TimeSlotId) üçlüye
    /// (CourseSectionId, TimeSlotId, ClassroomId) çevirir. Lab split senaryosunda
    /// aynı section aynı slotta farklı dersliklere atanabilsin diye gerekli.
    /// </summary>
    public partial class AllowLabSplitInSectionSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SectionSchedules_CourseSectionId_TimeSlotId",
                table: "SectionSchedules");

            migrationBuilder.CreateIndex(
                name: "IX_SectionSchedules_CourseSectionId_TimeSlotId_ClassroomId",
                table: "SectionSchedules",
                columns: new[] { "CourseSectionId", "TimeSlotId", "ClassroomId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SectionSchedules_CourseSectionId_TimeSlotId_ClassroomId",
                table: "SectionSchedules");

            migrationBuilder.CreateIndex(
                name: "IX_SectionSchedules_CourseSectionId_TimeSlotId",
                table: "SectionSchedules",
                columns: new[] { "CourseSectionId", "TimeSlotId" },
                unique: true);
        }
    }
}
