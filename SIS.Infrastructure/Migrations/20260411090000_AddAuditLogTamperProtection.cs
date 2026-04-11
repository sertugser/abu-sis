using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SIS.Infrastructure.Persistence;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260411090000_AddAuditLogTamperProtection")]
    public partial class AddAuditLogTamperProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_audit_log_tampering()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'AuditLog tablosunda güncelleme ve silme işlemleri yasaktır. Kayıt ID: %', OLD."Id";
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS trg_audit_log_no_update ON "AuditLogs";
                CREATE TRIGGER trg_audit_log_no_update
                    BEFORE UPDATE ON "AuditLogs"
                    FOR EACH ROW
                    EXECUTE FUNCTION prevent_audit_log_tampering();

                DROP TRIGGER IF EXISTS trg_audit_log_no_delete ON "AuditLogs";
                CREATE TRIGGER trg_audit_log_no_delete
                    BEFORE DELETE ON "AuditLogs"
                    FOR EACH ROW
                    EXECUTE FUNCTION prevent_audit_log_tampering();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_audit_log_no_delete ON "AuditLogs";
                DROP TRIGGER IF EXISTS trg_audit_log_no_update ON "AuditLogs";
                DROP FUNCTION IF EXISTS prevent_audit_log_tampering();
                """);
        }
    }
}
