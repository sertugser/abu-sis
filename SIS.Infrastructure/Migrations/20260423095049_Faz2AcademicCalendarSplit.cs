using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Faz2AcademicCalendarSplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Yeni TermAcademicCalendars tablosunu oluştur
            migrationBuilder.CreateTable(
                name: "TermAcademicCalendars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    RegistrationStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistrationEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AddDropStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AddDropEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WithdrawalDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GradeEntryStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GradeEntryEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistrationOverride = table.Column<bool>(type: "boolean", nullable: true),
                    AddDropOverride = table.Column<bool>(type: "boolean", nullable: true),
                    WithdrawalOverride = table.Column<bool>(type: "boolean", nullable: true),
                    GradeEntryOverride = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TermAcademicCalendars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TermAcademicCalendars_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TermAcademicCalendars_TermId",
                table: "TermAcademicCalendars",
                column: "TermId",
                unique: true);

            // 2) Mevcut AcademicTerms verilerini yeni tabloya kopyala (veri kaybını önler)
            migrationBuilder.Sql(@"
                INSERT INTO ""TermAcademicCalendars""
                    (""TermId"", ""RegistrationStart"", ""RegistrationEnd"",
                     ""AddDropStart"", ""AddDropEnd"", ""WithdrawalDeadline"",
                     ""GradeEntryStart"", ""GradeEntryEnd"",
                     ""RegistrationOverride"", ""AddDropOverride"",
                     ""WithdrawalOverride"", ""GradeEntryOverride"",
                     ""CreatedAt"")
                SELECT
                    ""Id"", ""RegistrationStart"", ""RegistrationEnd"",
                    ""AddDropStart"", ""AddDropEnd"", ""WithdrawalDeadline"",
                    ""GradeEntryStart"", ""GradeEntryEnd"",
                    ""RegistrationOverride"", ""AddDropOverride"",
                    ""WithdrawalOverride"", ""GradeEntryOverride"",
                    NOW() AT TIME ZONE 'UTC'
                FROM ""AcademicTerms"";
            ");

            // 3) Eski kolonları AcademicTerms'tan çıkar
            migrationBuilder.DropColumn(name: "AddDropEnd",           table: "AcademicTerms");
            migrationBuilder.DropColumn(name: "AddDropOverride",      table: "AcademicTerms");
            migrationBuilder.DropColumn(name: "AddDropStart",         table: "AcademicTerms");
            migrationBuilder.DropColumn(name: "GradeEntryEnd",        table: "AcademicTerms");
            migrationBuilder.DropColumn(name: "GradeEntryOverride",   table: "AcademicTerms");
            migrationBuilder.DropColumn(name: "GradeEntryStart",      table: "AcademicTerms");
            migrationBuilder.DropColumn(name: "RegistrationEnd",      table: "AcademicTerms");
            migrationBuilder.DropColumn(name: "RegistrationOverride", table: "AcademicTerms");
            migrationBuilder.DropColumn(name: "RegistrationStart",    table: "AcademicTerms");
            migrationBuilder.DropColumn(name: "WithdrawalDeadline",   table: "AcademicTerms");
            migrationBuilder.DropColumn(name: "WithdrawalOverride",   table: "AcademicTerms");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1) 11 kolonu AcademicTerms'a geri ekle
            migrationBuilder.AddColumn<DateTime>(
                name: "AddDropEnd",
                table: "AcademicTerms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "AddDropOverride",
                table: "AcademicTerms",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AddDropStart",
                table: "AcademicTerms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "GradeEntryEnd",
                table: "AcademicTerms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "GradeEntryOverride",
                table: "AcademicTerms",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GradeEntryStart",
                table: "AcademicTerms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationEnd",
                table: "AcademicTerms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "RegistrationOverride",
                table: "AcademicTerms",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationStart",
                table: "AcademicTerms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "WithdrawalDeadline",
                table: "AcademicTerms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "WithdrawalOverride",
                table: "AcademicTerms",
                type: "boolean",
                nullable: true);

            // 2) Veriyi TermAcademicCalendars'tan geri kopyala
            migrationBuilder.Sql(@"
                UPDATE ""AcademicTerms"" t SET
                    ""RegistrationStart""    = c.""RegistrationStart"",
                    ""RegistrationEnd""      = c.""RegistrationEnd"",
                    ""AddDropStart""         = c.""AddDropStart"",
                    ""AddDropEnd""           = c.""AddDropEnd"",
                    ""WithdrawalDeadline""   = c.""WithdrawalDeadline"",
                    ""GradeEntryStart""      = c.""GradeEntryStart"",
                    ""GradeEntryEnd""        = c.""GradeEntryEnd"",
                    ""RegistrationOverride"" = c.""RegistrationOverride"",
                    ""AddDropOverride""      = c.""AddDropOverride"",
                    ""WithdrawalOverride""   = c.""WithdrawalOverride"",
                    ""GradeEntryOverride""   = c.""GradeEntryOverride""
                FROM ""TermAcademicCalendars"" c
                WHERE c.""TermId"" = t.""Id"";
            ");

            // 3) Yeni tabloyu düşür
            migrationBuilder.DropTable(name: "TermAcademicCalendars");
        }
    }
}
