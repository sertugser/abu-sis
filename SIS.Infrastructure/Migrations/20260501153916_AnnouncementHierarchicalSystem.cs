using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AnnouncementHierarchicalSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetFacultyId",
                table: "InternalAnnouncements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetSectionId",
                table: "InternalAnnouncements",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternalAnnouncements_TargetFacultyId",
                table: "InternalAnnouncements",
                column: "TargetFacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalAnnouncements_TargetSectionId",
                table: "InternalAnnouncements",
                column: "TargetSectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_InternalAnnouncements_Faculties_TargetFacultyId",
                table: "InternalAnnouncements",
                column: "TargetFacultyId",
                principalTable: "Faculties",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InternalAnnouncements_CourseSections_TargetSectionId",
                table: "InternalAnnouncements",
                column: "TargetSectionId",
                principalTable: "CourseSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""InternalAnnouncements"" DROP CONSTRAINT IF EXISTS ""FK_InternalAnnouncements_Faculties_TargetFacultyId"";
                ALTER TABLE ""InternalAnnouncements"" DROP CONSTRAINT IF EXISTS ""FK_InternalAnnouncements_CourseSections_TargetSectionId"";
                DROP INDEX IF EXISTS ""IX_InternalAnnouncements_TargetFacultyId"";
                DROP INDEX IF EXISTS ""IX_InternalAnnouncements_TargetSectionId"";
                ALTER TABLE ""InternalAnnouncements"" DROP COLUMN IF EXISTS ""TargetFacultyId"";
                ALTER TABLE ""InternalAnnouncements"" DROP COLUMN IF EXISTS ""TargetSectionId"";
            ");
        }
    }
}
