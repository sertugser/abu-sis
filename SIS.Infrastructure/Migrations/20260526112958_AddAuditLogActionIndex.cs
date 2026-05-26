using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogActionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: index daha önce başka bir yolla oluşturulmuş olabilir (eşzamanlı migration drift'i).
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_AuditLogs_Timestamp_Action"" ON ""AuditLogs"" (""Timestamp"", ""Action"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Timestamp_Action",
                table: "AuditLogs");
        }
    }
}
