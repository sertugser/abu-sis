using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddElectiveSubtypesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ElectiveSubtypeId",
                table: "ElectiveGroups",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ElectiveSubtypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequiresFaculty = table.Column<bool>(type: "boolean", nullable: false),
                    ForbidsFaculty = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresDepartment = table.Column<bool>(type: "boolean", nullable: false),
                    ForbidsDepartment = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectiveSubtypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElectiveGroups_ElectiveSubtypeId",
                table: "ElectiveGroups",
                column: "ElectiveSubtypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectiveSubtypes_Code",
                table: "ElectiveSubtypes",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ElectiveGroups_ElectiveSubtypes_ElectiveSubtypeId",
                table: "ElectiveGroups",
                column: "ElectiveSubtypeId",
                principalTable: "ElectiveSubtypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ── Faz B B3a: 3 default subtype'ı id=1/2/3 olarak idempotent insert et ──
            // Karar #3: enum integer değerleri ID olarak birebir kopyalanır (1→1, 2→2, 3→3).
            // Idempotent — Code'su zaten varsa atlanır.
            migrationBuilder.Sql(@"
INSERT INTO ""ElectiveSubtypes"" (""Id"", ""Code"", ""Name"", ""RequiresFaculty"", ""ForbidsFaculty"", ""RequiresDepartment"", ""ForbidsDepartment"", ""IsActive"", ""SortOrder"", ""CreatedAt"")
SELECT 1, 'UniversityElective', 'Üniversite Seçmeli', false, true, false, true, true, 1, NOW()
WHERE NOT EXISTS (SELECT 1 FROM ""ElectiveSubtypes"" WHERE ""Code"" = 'UniversityElective');

INSERT INTO ""ElectiveSubtypes"" (""Id"", ""Code"", ""Name"", ""RequiresFaculty"", ""ForbidsFaculty"", ""RequiresDepartment"", ""ForbidsDepartment"", ""IsActive"", ""SortOrder"", ""CreatedAt"")
SELECT 2, 'NonDepartmentElective', 'Bölüm Dışı Seçmeli', false, false, false, false, true, 2, NOW()
WHERE NOT EXISTS (SELECT 1 FROM ""ElectiveSubtypes"" WHERE ""Code"" = 'NonDepartmentElective');

INSERT INTO ""ElectiveSubtypes"" (""Id"", ""Code"", ""Name"", ""RequiresFaculty"", ""ForbidsFaculty"", ""RequiresDepartment"", ""ForbidsDepartment"", ""IsActive"", ""SortOrder"", ""CreatedAt"")
SELECT 3, 'TechnicalElective', 'Teknik Seçmeli', true, false, false, false, true, 3, NOW()
WHERE NOT EXISTS (SELECT 1 FROM ""ElectiveSubtypes"" WHERE ""Code"" = 'TechnicalElective');
");

            // Backfill: mevcut ElectiveGroup.ElectiveSubtype enum integer değerini ID'ye kopyala.
            migrationBuilder.Sql(@"
UPDATE ""ElectiveGroups""
SET ""ElectiveSubtypeId"" = ""ElectiveSubtype""
WHERE ""ElectiveSubtypeId"" IS NULL;
");

            // PostgreSQL identity sequence'ini, manuel insert edilen 1/2/3 ID'lerinin üstüne ayarla.
            migrationBuilder.Sql(@"
SELECT setval(pg_get_serial_sequence('""ElectiveSubtypes""', 'Id'), COALESCE((SELECT MAX(""Id"") FROM ""ElectiveSubtypes""), 0) + 1, false);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback: önce backfill'i temizle (FK'i drop edebilmek için).
            migrationBuilder.Sql(@"
UPDATE ""ElectiveGroups""
SET ""ElectiveSubtypeId"" = NULL;
");

            migrationBuilder.DropForeignKey(
                name: "FK_ElectiveGroups_ElectiveSubtypes_ElectiveSubtypeId",
                table: "ElectiveGroups");

            migrationBuilder.DropIndex(
                name: "IX_ElectiveGroups_ElectiveSubtypeId",
                table: "ElectiveGroups");

            migrationBuilder.DropColumn(
                name: "ElectiveSubtypeId",
                table: "ElectiveGroups");

            migrationBuilder.DropTable(
                name: "ElectiveSubtypes");
        }
    }
}
