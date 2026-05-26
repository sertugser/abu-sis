using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportAccessGrantUnifiedModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportAccessGrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScopeKind = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<int>(type: "integer", nullable: false),
                    SubjectKind = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    Effect = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrantedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportAccessGrants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportUserGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportUserGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportUserGroupMembers",
                columns: table => new
                {
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportUserGroupMembers", x => new { x.GroupId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ReportUserGroupMembers_ReportUserGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ReportUserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportAccessGrants_ScopeKind_TargetId_SubjectKind_SubjectId",
                table: "ReportAccessGrants",
                columns: new[] { "ScopeKind", "TargetId", "SubjectKind", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportAccessGrants_SubjectKind_SubjectId",
                table: "ReportAccessGrants",
                columns: new[] { "SubjectKind", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportUserGroupMembers_UserId",
                table: "ReportUserGroupMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportUserGroups_Name",
                table: "ReportUserGroups",
                column: "Name",
                unique: true);

            // ── Eski rol-bazlı yetkiyi birleşik grant tablosuna taşı (eski tablolar drop'tan ÖNCE) ──
            // Kategori-rol → grant (ScopeKind=0 Category, SubjectKind=0 Role, Effect=0 Allow)
            migrationBuilder.Sql(@"
                INSERT INTO ""ReportAccessGrants""
                    (""ScopeKind"", ""TargetId"", ""SubjectKind"", ""SubjectId"", ""Effect"", ""Reason"", ""GrantedAt"", ""GrantedByUserId"")
                SELECT 0, rp.""CategoryId"", 0, rp.""RoleId"", 0, NULL, rp.""GrantedAt"", rp.""GrantedByUserId""
                FROM ""ReportCategoryRolePermissions"" rp
                ON CONFLICT DO NOTHING;");

            // Rapor-rol override → grant (ScopeKind=1 Report, SubjectKind=0 Role, Effect = IsAllowed ? 0 : 1)
            migrationBuilder.Sql(@"
                INSERT INTO ""ReportAccessGrants""
                    (""ScopeKind"", ""TargetId"", ""SubjectKind"", ""SubjectId"", ""Effect"", ""Reason"", ""GrantedAt"", ""GrantedByUserId"")
                SELECT 1, o.""ReportId"", 0, o.""RoleId"", CASE WHEN o.""IsAllowed"" THEN 0 ELSE 1 END, o.""Reason"", o.""CreatedAt"", o.""CreatedByUserId""
                FROM ""ReportPermissionOverrides"" o
                ON CONFLICT DO NOTHING;");

            // Veri taşındı → eski tabloları kaldır
            migrationBuilder.DropTable(name: "ReportPermissionOverrides");
            migrationBuilder.DropTable(name: "ReportCategoryRolePermissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportAccessGrants");

            migrationBuilder.DropTable(
                name: "ReportUserGroupMembers");

            migrationBuilder.DropTable(
                name: "ReportUserGroups");

            migrationBuilder.CreateTable(
                name: "ReportCategoryRolePermissions",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrantedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportCategoryRolePermissions", x => new { x.CategoryId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_ReportCategoryRolePermissions_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReportCategoryRolePermissions_ReportCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ReportCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReportPermissionOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReportId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    IsAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RoleId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportPermissionOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportPermissionOverrides_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReportPermissionOverrides_ReportDefinitions_ReportId",
                        column: x => x.ReportId,
                        principalTable: "ReportDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportCategoryRolePermissions_RoleId",
                table: "ReportCategoryRolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportPermissionOverrides_ReportId_RoleId",
                table: "ReportPermissionOverrides",
                columns: new[] { "ReportId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportPermissionOverrides_RoleId",
                table: "ReportPermissionOverrides",
                column: "RoleId");
        }
    }
}
