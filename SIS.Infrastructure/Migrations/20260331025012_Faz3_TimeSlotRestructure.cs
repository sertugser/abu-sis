using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Faz3_TimeSlotRestructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eski pattern bazlı seed data'yı temizle (yeni yapıya uymuyor)
            migrationBuilder.Sql("""DELETE FROM "TimeSlots";""");

            migrationBuilder.DropIndex(
                name: "IX_TimeSlots_Pattern_StartTime_EndTime",
                table: "TimeSlots");

            migrationBuilder.RenameColumn(
                name: "SlotType",
                table: "TimeSlots",
                newName: "TermId");

            migrationBuilder.RenameColumn(
                name: "Pattern",
                table: "TimeSlots",
                newName: "SlotOrder");

            migrationBuilder.AddColumn<int>(
                name: "DayOfWeek",
                table: "TimeSlots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_TimeSlots_TermId_DayOfWeek_StartTime",
                table: "TimeSlots",
                columns: new[] { "TermId", "DayOfWeek", "StartTime" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TimeSlots_AcademicTerms_TermId",
                table: "TimeSlots",
                column: "TermId",
                principalTable: "AcademicTerms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TimeSlots_AcademicTerms_TermId",
                table: "TimeSlots");

            migrationBuilder.DropIndex(
                name: "IX_TimeSlots_TermId_DayOfWeek_StartTime",
                table: "TimeSlots");

            migrationBuilder.DropColumn(
                name: "DayOfWeek",
                table: "TimeSlots");

            migrationBuilder.RenameColumn(
                name: "TermId",
                table: "TimeSlots",
                newName: "SlotType");

            migrationBuilder.RenameColumn(
                name: "SlotOrder",
                table: "TimeSlots",
                newName: "Pattern");

            migrationBuilder.CreateIndex(
                name: "IX_TimeSlots_Pattern_StartTime_EndTime",
                table: "TimeSlots",
                columns: new[] { "Pattern", "StartTime", "EndTime" },
                unique: true);
        }
    }
}
