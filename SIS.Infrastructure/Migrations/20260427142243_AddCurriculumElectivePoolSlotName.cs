using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCurriculumElectivePoolSlotName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CurriculumElectivePools_CurriculumId_ElectiveGroupId",
                table: "CurriculumElectivePools");

            migrationBuilder.AddColumn<string>(
                name: "SlotName",
                table: "CurriculumElectivePools",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumElectivePools_CurriculumId_ElectiveGroupId_SlotNa~",
                table: "CurriculumElectivePools",
                columns: new[] { "CurriculumId", "ElectiveGroupId", "SlotName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CurriculumElectivePools_CurriculumId_ElectiveGroupId_SlotNa~",
                table: "CurriculumElectivePools");

            migrationBuilder.DropColumn(
                name: "SlotName",
                table: "CurriculumElectivePools");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumElectivePools_CurriculumId_ElectiveGroupId",
                table: "CurriculumElectivePools",
                columns: new[] { "CurriculumId", "ElectiveGroupId" },
                unique: true);
        }
    }
}
