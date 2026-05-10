using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CleanupOrphanInstructorMaxWeeklyHoursKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "SystemConfigs"
                WHERE "Key" IN (
                    'Instructor.MaxWeeklyHours.Professor',
                    'Instructor.MaxWeeklyHours.AssociateProfessor',
                    'Instructor.MaxWeeklyHours.AssistantProfessor',
                    'Instructor.MaxWeeklyHours.Lecturer'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "SystemConfigs" ("Key", "Value", "Category", "DataType", "Description")
                VALUES
                    ('Instructor.MaxWeeklyHours.Professor', '12', 'Scheduling', 'int', 'Profesör max haftalık ders saati'),
                    ('Instructor.MaxWeeklyHours.AssociateProfessor', '16', 'Scheduling', 'int', 'Doçent max haftalık ders saati'),
                    ('Instructor.MaxWeeklyHours.AssistantProfessor', '20', 'Scheduling', 'int', 'Dr. Öğr. Üyesi max haftalık ders saati'),
                    ('Instructor.MaxWeeklyHours.Lecturer', '24', 'Scheduling', 'int', 'Öğretim Görevlisi max haftalık ders saati');
                """);
        }
    }
}
