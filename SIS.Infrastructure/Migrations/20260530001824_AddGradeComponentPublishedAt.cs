using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGradeComponentPublishedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "GradeComponents",
                type: "timestamp with time zone",
                nullable: true);

            // Geriye uyumluluk: zaten finalize edilmiş section'ların komponentlerini
            // section.UpdatedAt ile ilan edilmiş say. Aksi halde mevcut öğrencilerin
            // gördüğü notlar bu migration sonrası aniden kaybolurdu.
            migrationBuilder.Sql(@"
                UPDATE ""GradeComponents"" gc
                SET ""PublishedAt"" = COALESCE(cs.""UpdatedAt"", NOW())
                FROM ""CourseSections"" cs
                WHERE cs.""CourseId"" = gc.""CourseId""
                  AND cs.""TermId"" = gc.""AcademicTermId""
                  AND cs.""IsFinalized"" = TRUE
                  AND gc.""PublishedAt"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "GradeComponents");
        }
    }
}
