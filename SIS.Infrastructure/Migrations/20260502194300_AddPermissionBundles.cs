using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionBundles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_RolePermissions",
                table: "RolePermissions");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "RolePermissions",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "SourceBundleId",
                table: "RolePermissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_RolePermissions",
                table: "RolePermissions",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "PermissionBundles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsSystemDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionBundles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermissionBundleItems",
                columns: table => new
                {
                    BundleId = table.Column<int>(type: "integer", nullable: false),
                    PermissionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionBundleItems", x => new { x.BundleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_PermissionBundleItems_PermissionBundles_BundleId",
                        column: x => x.BundleId,
                        principalTable: "PermissionBundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PermissionBundleItems_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_bundle_unique",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId", "SourceBundleId" },
                unique: true,
                filter: "\"SourceBundleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_manual_unique",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true,
                filter: "\"SourceBundleId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId",
                table: "RolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_SourceBundleId",
                table: "RolePermissions",
                column: "SourceBundleId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionBundleItems_PermissionId",
                table: "PermissionBundleItems",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionBundles_Category",
                table: "PermissionBundles",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionBundles_Name",
                table: "PermissionBundles",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_PermissionBundles_SourceBundleId",
                table: "RolePermissions",
                column: "SourceBundleId",
                principalTable: "PermissionBundles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_PermissionBundles_SourceBundleId",
                table: "RolePermissions");

            migrationBuilder.DropTable(
                name: "PermissionBundleItems");

            migrationBuilder.DropTable(
                name: "PermissionBundles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RolePermissions",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "ix_role_permissions_bundle_unique",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "ix_role_permissions_manual_unique",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_RoleId",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_SourceBundleId",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "SourceBundleId",
                table: "RolePermissions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RolePermissions",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" });
        }
    }
}
