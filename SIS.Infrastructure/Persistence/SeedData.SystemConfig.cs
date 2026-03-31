using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    public static async Task SeedSystemConfigAsync(AppDbContext context)
    {
        if (await context.SystemConfigs.AnyAsync()) return;

        var configs = new List<SystemConfig>
        {
            // ── Kayıt ECTS Limitleri ─────────────────────────────
            new() { Key = "Registration.EctsLimit.CgpaBelow200", Value = "30", Category = "Registration", DataType = "int", Description = "CGPA < 2.00 olan öğrencilerin alabileceği max AKTS" },
            new() { Key = "Registration.EctsLimit.Cgpa200To249", Value = "42", Category = "Registration", DataType = "int", Description = "CGPA 2.00-2.49 arası öğrencilerin alabileceği max AKTS" },
            new() { Key = "Registration.EctsLimit.CgpaAbove250", Value = "48", Category = "Registration", DataType = "int", Description = "CGPA >= 2.50 olan öğrencilerin alabileceği max AKTS" },

            // ── GPA Eşikleri ──────────────────────────────────────
            new() { Key = "GPA.RetentionThreshold", Value = "1.70", Category = "GPA", DataType = "decimal", Description = "Bu CGPA altındaki öğrenciler sınıfta kalır (1. dönem sonrası)" },

            // ── Ders Çekilme Limitleri ────────────────────────────
            new() { Key = "Withdrawal.MaxCount.Undergraduate", Value = "6", Category = "Registration", DataType = "int", Description = "Lisans öğrencileri için toplam çekilme hakkı" },
            new() { Key = "Withdrawal.MaxCount.Associate", Value = "3", Category = "Registration", DataType = "int", Description = "Önlisans öğrencileri için toplam çekilme hakkı" },

            // ── YÖK Unvan → MaxWeeklyHours ───────────────────────
            new() { Key = "Instructor.MaxWeeklyHours.Professor", Value = "12", Category = "Scheduling", DataType = "int", Description = "Profesör max haftalık ders saati" },
            new() { Key = "Instructor.MaxWeeklyHours.AssociateProfessor", Value = "16", Category = "Scheduling", DataType = "int", Description = "Doçent max haftalık ders saati" },
            new() { Key = "Instructor.MaxWeeklyHours.AssistantProfessor", Value = "20", Category = "Scheduling", DataType = "int", Description = "Dr. Öğr. Üyesi max haftalık ders saati" },
            new() { Key = "Instructor.MaxWeeklyHours.Lecturer", Value = "24", Category = "Scheduling", DataType = "int", Description = "Öğretim Görevlisi max haftalık ders saati" },

            // ── Derslik Eşikleri ──────────────────────────────────
            new() { Key = "Classroom.AmphiThreshold", Value = "100", Category = "Scheduling", DataType = "int", Description = "Bu sayıdan fazla kontenjan → Amfi zorunlu" },
            new() { Key = "Classroom.OccupancyTarget.Min", Value = "60", Category = "Scheduling", DataType = "int", Description = "Derslik doluluk oranı alt hedefi (%)" },
            new() { Key = "Classroom.OccupancyTarget.Max", Value = "85", Category = "Scheduling", DataType = "int", Description = "Derslik doluluk oranı üst hedefi (%)" },
        };

        context.SystemConfigs.AddRange(configs);
        await context.SaveChangesAsync();
    }
}
