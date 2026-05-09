using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MergeSchedulingAlgorithmConfigsIntoSystemConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 2026-05-09 — Dual-table desync fix: SchedulingAlgorithmConfigs satırlarını
            // SystemConfigs'e taşı (key conflict varsa SystemConfigs öncelikli — solver
            // zaten oradan okuduğu için "kaynak" rolü oradadır). Ardından eski tabloları drop et.
            // SchedulingAlgorithmConfigGroups veri taşımıyor — UI metadatası artık controller'da
            // hardcoded.
            migrationBuilder.Sql(@"
                INSERT INTO ""SystemConfigs"" (""Key"", ""Value"", ""Description"", ""Category"", ""DataType"")
                SELECT s.""Key"", s.""Value"", s.""Description"", s.""Category"", s.""DataType""
                FROM ""SchedulingAlgorithmConfigs"" s
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""SystemConfigs"" sc WHERE sc.""Key"" = s.""Key""
                );
            ");

            migrationBuilder.DropTable(
                name: "SchedulingAlgorithmConfigGroups");

            migrationBuilder.DropTable(
                name: "SchedulingAlgorithmConfigs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchedulingAlgorithmConfigGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BadgeClass = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingAlgorithmConfigGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingAlgorithmConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Category = table.Column<string>(type: "text", nullable: false),
                    DataType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Key = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingAlgorithmConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingAlgorithmConfigGroups_Category",
                table: "SchedulingAlgorithmConfigGroups",
                column: "Category",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingAlgorithmConfigs_Key",
                table: "SchedulingAlgorithmConfigs",
                column: "Key",
                unique: true);
        }
    }
}
