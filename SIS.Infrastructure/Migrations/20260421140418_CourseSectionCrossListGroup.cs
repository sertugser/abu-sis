using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CourseSectionCrossListGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CrossListGroupId",
                table: "CourseSections",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseSections_CrossListGroupId",
                table: "CourseSections",
                column: "CrossListGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CourseSections_CrossListGroupId",
                table: "CourseSections");

            migrationBuilder.DropColumn(
                name: "CrossListGroupId",
                table: "CourseSections");
        }
    }
}
