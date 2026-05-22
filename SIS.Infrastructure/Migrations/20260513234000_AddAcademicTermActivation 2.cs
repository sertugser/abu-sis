using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SIS.Infrastructure.Persistence;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260513234000_AddAcademicTermActivation")]
    public partial class AddAcademicTermActivation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActivated",
                table: "AcademicTerms",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActivated",
                table: "AcademicTerms");
        }
    }
}
