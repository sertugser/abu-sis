using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    public static async Task SeedWebModuleSettingsAsync(AppDbContext context)
    {
        var settings = new List<WebModuleSetting>
        {
            new()
            {
                ModuleKey = "StudentPanel",
                Key = "CourseRegistrationEnabled",
                Value = "true",
                Category = "Registration",
                DataType = "bool",
                Description = "Öğrenci panelinde ders kayıt işlemleri aktif mi?"
            },
            new()
            {
                ModuleKey = "StudentPanel",
                Key = "CourseRegistrationOpenAt",
                Value = "09:00",
                Category = "Registration",
                DataType = "time",
                Description = "Öğrenci panelinde ders kayıt ekranının günlük açılış saati"
            },
            new()
            {
                ModuleKey = "StudentPanel",
                Key = "DocumentRequestEnabled",
                Value = "true",
                Category = "Documents",
                DataType = "bool",
                Description = "Öğrenci panelinde belge talebi butonu aktif mi?"
            },
            new()
            {
                ModuleKey = "InstructorPanel",
                Key = "ShowStudentPhone",
                Value = "false",
                Category = "Privacy",
                DataType = "bool",
                Description = "Akademisyen ekranlarında öğrenci telefon numarası gösterilsin mi?"
            },
            new()
            {
                ModuleKey = "StaffPanel",
                Key = "DocumentWorkflowEnabled",
                Value = "true",
                Category = "Documents",
                DataType = "bool",
                Description = "Personel panelinde belge iş akışı aktif mi?"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "StudentTopCount",
                Value = "5",
                Category = "Announcements",
                DataType = "int",
                Description = "Öğrenci panelinde üstte gösterilecek duyuru sayısı"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "PinnedAnnouncementsFirst",
                Value = "true",
                Category = "Announcements",
                DataType = "bool",
                Description = "Sabitlenmiş duyurular listelerde önce gösterilsin mi?"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "MaxTitleLength",
                Value = "200",
                Category = "Announcements",
                DataType = "int",
                Description = "Duyuru başlığı için izin verilen maksimum karakter sayısı"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "MaxContentLength",
                Value = "50000",
                Category = "Announcements",
                DataType = "int",
                Description = "Duyuru içeriği için izin verilen maksimum karakter sayısı (HTML dahil)"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "AllowImagesInContent",
                Value = "true",
                Category = "Announcements",
                DataType = "bool",
                Description = "Zengin metin içeriğinde resim eklenebilir mi?"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "AllowTablesInContent",
                Value = "true",
                Category = "Announcements",
                DataType = "bool",
                Description = "Zengin metin içeriğinde tablo eklenebilir mi?"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "StudentYearFilterMin",
                Value = "1",
                Category = "Announcements",
                DataType = "int",
                Description = "Sınıf bazlı duyuru hedeflemesinde seçilebilecek en küçük sınıf yılı (örn. 0 = Hazırlık)"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "StudentYearFilterMax",
                Value = "7",
                Category = "Announcements",
                DataType = "int",
                Description = "Sınıf bazlı duyuru hedeflemesinde seçilebilecek en büyük sınıf yılı"
            },
        };

        var existingPairs = await context.WebModuleSettings
            .AsNoTracking()
            .Select(setting => new { setting.ModuleKey, setting.Key })
            .ToListAsync();

        var existingSet = existingPairs
            .Select(pair => $"{pair.ModuleKey}:{pair.Key}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingSettings = settings
            .Where(setting => !existingSet.Contains($"{setting.ModuleKey}:{setting.Key}"))
            .ToList();

        if (missingSettings.Count == 0)
            return;

        context.WebModuleSettings.AddRange(missingSettings);
        await context.SaveChangesAsync();
    }
}
