using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnouncementCategoriesAndCategoryFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "InternalAnnouncements",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnnouncementCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ColorHex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    IconName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InternalAnnouncements_CategoryId",
                table: "InternalAnnouncements",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementCategories_Name",
                table: "AnnouncementCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementCategories_SortOrder",
                table: "AnnouncementCategories",
                column: "SortOrder");

            migrationBuilder.AddForeignKey(
                name: "FK_InternalAnnouncements_AnnouncementCategories_CategoryId",
                table: "InternalAnnouncements",
                column: "CategoryId",
                principalTable: "AnnouncementCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InternalAnnouncements_AnnouncementCategories_CategoryId",
                table: "InternalAnnouncements");

            migrationBuilder.DropTable(
                name: "AnnouncementCategories");

            migrationBuilder.DropIndex(
                name: "IX_InternalAnnouncements_CategoryId",
                table: "InternalAnnouncements");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "InternalAnnouncements");
        }
    }
}
