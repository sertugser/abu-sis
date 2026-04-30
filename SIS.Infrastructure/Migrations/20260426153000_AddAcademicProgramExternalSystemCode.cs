using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SIS.Infrastructure.Persistence;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260426153000_AddAcademicProgramExternalSystemCode")]
    public partial class AddAcademicProgramExternalSystemCode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalSystemCode",
                table: "AcademicPrograms",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicPrograms_ExternalSystemCode",
                table: "AcademicPrograms",
                column: "ExternalSystemCode",
                unique: true,
                filter: "\"ExternalSystemCode\" IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AcademicPrograms_ExternalSystemCode",
                table: "AcademicPrograms");

            migrationBuilder.DropColumn(
                name: "ExternalSystemCode",
                table: "AcademicPrograms");
        }
    }
}
