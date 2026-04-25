using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuModuleSlugAndRelaxRoute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MenuItems_Route",
                table: "MenuItems");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "MenuModules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MenuModules_Slug",
                table: "MenuModules",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_ModuleId_Route",
                table: "MenuItems",
                columns: new[] { "ModuleId", "Route" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_Route",
                table: "MenuItems",
                column: "Route");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MenuModules_Slug",
                table: "MenuModules");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_ModuleId_Route",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_Route",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "MenuModules");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_Route",
                table: "MenuItems",
                column: "Route",
                unique: true);
        }
    }
}
