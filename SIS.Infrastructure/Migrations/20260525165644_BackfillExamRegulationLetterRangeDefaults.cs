using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillExamRegulationLetterRangeDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rows that existed before AddExamRegulationLetterRanges got EF's value-type
            // defaults (all thresholds 0, LetterRangesEditable false). Backfill them to the
            // intended defaults so a locked regulation never resolves an all-zero letter table.
            migrationBuilder.Sql(@"
                UPDATE ""ExamRegulations""
                SET ""AAMin"" = 91, ""BAMin"" = 81, ""BBMin"" = 71,
                    ""CBMin"" = 61, ""CCMin"" = 55, ""DCMin"" = 50,
                    ""DDMin"" = 45, ""LetterRangesEditable"" = true
                WHERE ""AAMin"" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
