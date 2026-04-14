using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CalendarGenerator.DTOs;
using SIS.Application.CalendarGenerator.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.CalendarGenerator;

public class CalendarTemplateService : ICalendarTemplateService
{
    private readonly AppDbContext _context;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public CalendarTemplateService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CalendarTemplateDto>> GetTemplatesAsync(CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);

        var templates = await _context.CalendarTemplates
            .AsNoTracking()
            .OrderBy(t => t.ChainCode)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);

        return templates.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<CalendarTemplateDto>> ReplaceTemplatesAsync(IReadOnlyList<UpsertCalendarTemplateDto> templates, CancellationToken ct = default)
    {
        var existing = await _context.CalendarTemplates.ToListAsync(ct);
        _context.CalendarTemplates.RemoveRange(existing);

        var now = DateTime.UtcNow;
        foreach (var dto in templates)
        {
            _context.CalendarTemplates.Add(new CalendarTemplate
            {
                Name = dto.Name.Trim(),
                ChainCode = dto.ChainCode.Trim().ToUpperInvariant(),
                IsActive = dto.IsActive,
                ItemsJson = JsonSerializer.Serialize(dto.Items.OrderBy(i => i.SortOrder), JsonOptions),
                CreatedAt = now
            });
        }

        await _context.SaveChangesAsync(ct);
        return await GetTemplatesAsync(ct);
    }

    public async Task<IReadOnlyList<HolidayDefinitionDto>> GetHolidayDefinitionsAsync(CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);

        var definitions = await _context.HolidayDefinitions
            .AsNoTracking()
            .OrderBy(h => h.Type)
            .ThenBy(h => h.FixedMonth ?? 99)
            .ThenBy(h => h.FixedDay ?? 99)
            .ThenBy(h => h.Name)
            .ToListAsync(ct);

        return definitions.Select(ToDto).ToList();
    }

    public async Task<HolidayDefinitionDto> UpsertHolidayDefinitionAsync(int? id, UpsertHolidayDefinitionDto dto, CancellationToken ct = default)
    {
        HolidayDefinition entity;
        var now = DateTime.UtcNow;

        if (id.HasValue)
        {
            entity = await _context.HolidayDefinitions.FindAsync([id.Value], ct)
                ?? throw new KeyNotFoundException($"Holiday definition not found: {id.Value}");
            entity.UpdatedAt = now;
        }
        else
        {
            entity = new HolidayDefinition { CreatedAt = now };
            _context.HolidayDefinitions.Add(entity);
        }

        entity.Name = dto.Name.Trim();
        entity.EnglishName = dto.EnglishName.Trim();
        entity.Type = dto.Type;
        entity.FixedMonth = dto.FixedMonth;
        entity.FixedDay = dto.FixedDay;
        entity.HijriMonth = dto.HijriMonth;
        entity.HijriDay = dto.HijriDay;
        entity.DurationDays = dto.DurationDays;
        entity.IsHalfDayStart = dto.IsHalfDayStart;
        entity.IsOfficialHoliday = dto.IsOfficialHoliday;
        entity.BlocksAcademicEvents = dto.BlocksAcademicEvents;
        entity.Source = dto.Source.Trim();
        entity.OverridesJson = JsonSerializer.Serialize(dto.Overrides, JsonOptions);
        entity.IsActive = dto.IsActive;

        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task EnsureDefaultsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        if (!await _context.CalendarTemplates.AnyAsync(ct))
        {
            _context.CalendarTemplates.AddRange(DefaultTemplates(now));
            await _context.SaveChangesAsync(ct);
        }

        if (!await _context.HolidayDefinitions.AnyAsync(ct))
        {
            _context.HolidayDefinitions.AddRange(DefaultHolidayDefinitions(now));
            await _context.SaveChangesAsync(ct);
        }
    }

    private static CalendarTemplateDto ToDto(CalendarTemplate entity)
    {
        var items = DeserializeItems(entity.ItemsJson);
        return new CalendarTemplateDto(entity.Id, entity.Name, entity.ChainCode, entity.IsActive, items);
    }

    private static HolidayDefinitionDto ToDto(HolidayDefinition entity)
    {
        var overrides = DeserializeOverrides(entity.OverridesJson);
        return new HolidayDefinitionDto(
            entity.Id,
            entity.Name,
            entity.EnglishName,
            entity.Type,
            entity.FixedMonth,
            entity.FixedDay,
            entity.HijriMonth,
            entity.HijriDay,
            entity.DurationDays,
            entity.IsHalfDayStart,
            entity.IsOfficialHoliday,
            entity.BlocksAcademicEvents,
            entity.Source,
            overrides,
            entity.IsActive);
    }

    internal static IReadOnlyList<CalendarTemplateItemDto> DeserializeItems(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<CalendarTemplateItemDto>>(json, JsonOptions) ?? [];
    }

    internal static IReadOnlyList<HolidayOverrideDto> DeserializeOverrides(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        if (json.TrimStart().StartsWith("{"))
        {
            var map = JsonSerializer.Deserialize<Dictionary<int, HolidayOverrideDto>>(json, JsonOptions);
            return map?.Values.OrderBy(v => v.Year).ToList() ?? [];
        }

        return JsonSerializer.Deserialize<List<HolidayOverrideDto>>(json, JsonOptions) ?? [];
    }

    internal static string SerializeItems(IEnumerable<CalendarTemplateItemDto> items) =>
        JsonSerializer.Serialize(items.OrderBy(i => i.SortOrder), JsonOptions);

    internal static string SerializeOverrides(IEnumerable<HolidayOverrideDto> overrides) =>
        JsonSerializer.Serialize(overrides.OrderBy(o => o.Year), JsonOptions);

    private static IEnumerable<CalendarTemplate> DefaultTemplates(DateTime now)
    {
        return
        [
            new()
            {
                Name = "Ana Akademik Surec",
                ChainCode = "MAIN",
                IsActive = true,
                ItemsJson = SerializeItems(DefaultMainItems()),
                CreatedAt = now
            },
            new()
            {
                Name = "Basvuru ve Kayit Surecleri",
                ChainCode = "ADMISSIONS",
                IsActive = true,
                ItemsJson = SerializeItems(DefaultAdmissionsItems()),
                CreatedAt = now
            },
            new()
            {
                Name = "Yaz Ogretimi",
                ChainCode = "SUMMER",
                IsActive = true,
                ItemsJson = SerializeItems(DefaultSummerItems()),
                CreatedAt = now
            }
        ];
    }

    private static IEnumerable<CalendarTemplateItemDto> DefaultMainItems()
    {
        return
        [
            Item("classes_start", CalendarEventType.ClassesStart, "Derslerin Baslamasi", 1, 0, null, SemesterType.Fall, SemesterType.Spring),
            Item("add_drop", CalendarEventType.AddDrop, "Ders Ekleme - Birakma, Gec Kayitlar ve Danisman Onaylari", 5, 7, null, SemesterType.Fall, SemesterType.Spring),
            Item("withdrawal_deadline", CalendarEventType.CourseWithdrawal, "Dersten Cekilme Basvurularinin Son Gunu", 1, 40, null, SemesterType.Fall, SemesterType.Spring),
            Item("freeze_deadline", CalendarEventType.RegistrationFreezeDeadline, "Kayit Dondurma Basvurularinin Son Gunu", 1, 40, null, SemesterType.Fall, SemesterType.Spring),
            Item("midterm_exam", CalendarEventType.MidtermExam, "Ara Sinavlar", 6, 43, null, SemesterType.Fall, SemesterType.Spring),
            Item("midterm_excuse_application", CalendarEventType.ExcuseExamApplication, "Mazeret Sinav Basvurularinin Son Gunu (Ara Sinav)", 1, 50, null, SemesterType.Fall, SemesterType.Spring),
            Item("midterm_excuse_exam", CalendarEventType.ExcuseExam, "Mazeret Sinavlari (Ara Sinav)", 3, 52, null, SemesterType.Fall, SemesterType.Spring),
            Item("midterm_publication", CalendarEventType.MidtermGradePublication, "Ara Sinavlarin Ilaninin Son Gunu", 1, 60, null, SemesterType.Fall, SemesterType.Spring),
            Item("classes_end", CalendarEventType.ClassesEnd, "Derslerin Sona Ermesi", 1, 102, null, SemesterType.Fall, SemesterType.Spring),
            Item("final_exam", CalendarEventType.FinalExam, "Donem Sonu Sinavlari", 12, 105, null, SemesterType.Fall, SemesterType.Spring),
            Item("prep_final_exam", CalendarEventType.PrepSchoolExam, "Hazirlik (Ingilizce) Muafiyet Sinavlari", 4, 112, null, SemesterType.Fall, SemesterType.Spring),
            Item("final_publication", CalendarEventType.FinalGradePublication, "Donem Sonu Sinav Sonuclarinin Ilani", 1, 121, null, SemesterType.Fall, SemesterType.Spring),
            Item("makeup_application", CalendarEventType.MakeupExam, "Butunleme Sinavlarinin Basvurularinin Son Gunu (Donem Sonu)", 1, 123, null, SemesterType.Fall, SemesterType.Spring),
            Item("makeup_exam", CalendarEventType.MakeupExam, "Butunleme Sinavlari", 6, 126, null, SemesterType.Fall, SemesterType.Spring),
            Item("makeup_publication", CalendarEventType.MakeupGradePublication, "Butunleme Sinav Sonuclarinin Ilani Icin Son Gun", 1, 136, null, SemesterType.Fall, SemesterType.Spring),
            Item("fall_semester_break", CalendarEventType.SemesterBreak, "Yariyil Tatili", 22, 130, null, SemesterType.Fall)
        ];
    }

    private static IEnumerable<CalendarTemplateItemDto> DefaultAdmissionsItems()
    {
        return
        [
            Item("fall_early_payment", CalendarEventType.TuitionPayment, "Erken Odemeler", 10, -89, null, SemesterType.Fall),
            Item("fall_international", CalendarEventType.InternationalStudentApplication, "On Lisans ve Lisans Programlarina Uluslararasi Ogrenci Basvuru ve Kayitlari", 151, -83, null, SemesterType.Fall),
            Item("fall_transfer_app", CalendarEventType.HorizontalTransfer, "Yatay Gecis Basvurulari", 15, -52, null, SemesterType.Fall),
            Item("fall_double_major", CalendarEventType.DoubleMajor, "Cift Anadal ve Yandal Programi Basvurulari", 15, -52, null, SemesterType.Fall),
            Item("fall_graduate_app", CalendarEventType.GraduateApplication, "Lisansustu Programlari Basvurulari", 39, -49, null, SemesterType.Fall),
            Item("fall_tuition", CalendarEventType.TuitionPayment, "Ogrenim Ucretlerinin Odenmesi", 12, -49, null, SemesterType.Fall),
            Item("fall_transfer_eval", CalendarEventType.TransferEvaluation, "Yatay Gecis Basvurularinin Degerlendirilmesi", 15, -35, null, SemesterType.Fall),
            Item("fall_transfer_result", CalendarEventType.TransferResult, "Yatay Gecis, Cift Anadal ve Yandal Sonuclarinin Ilani", 1, -17, null, SemesterType.Fall),
            Item("fall_transfer_registration_main", CalendarEventType.TransferRegistration, "Yatay Gecis Kabul Edilen Ogrencilerin Kayitlari (Asil)", 3, -14, null, SemesterType.Fall),
            Item("fall_transfer_registration_wait", CalendarEventType.TransferRegistration, "Yatay Gecis Kabul Edilen Ogrencilerin Kayitlari (Yedek)", 2, -11, null, SemesterType.Fall),
            Item("fall_prep_exam", CalendarEventType.PrepSchoolExam, "Hazirlik (Ingilizce) Muafiyet Sinavlari", 4, -13, null, SemesterType.Fall),
            Item("fall_exemption_result", CalendarEventType.CourseExemptionResult, "Ders Muafiyet Sonuclarinin Ilan Edilmesi", 1, -10, null, SemesterType.Fall),
            Item("fall_graduate_result", CalendarEventType.GraduateResult, "Lisansustu Program Basvuru Sonuclarinin Ilani", 1, -10, null, SemesterType.Fall),
            Item("fall_orientation", CalendarEventType.Orientation, "Yeni Kayit Olan Ogrenciler icin Oryantasyon Egitimi", 1, -7, null, SemesterType.Fall),
            Item("fall_course_registration", CalendarEventType.CourseRegistration, "Ders Kayit ve Danisman Onaylari", 5, -7, null, SemesterType.Fall),
            Item("fall_graduate_registration", CalendarEventType.GraduateRegistration, "Lisansustu Programlari Ucret Odeme, Ders Kayitlari ve Danisman Onaylari", 5, -7, null, SemesterType.Fall),
            Item("fall_scholarship_app", CalendarEventType.ScholarshipApplication, "Burs Basvurularinin Alinmasi", 12, 7, null, SemesterType.Fall),
            Item("fall_scholarship_result", CalendarEventType.ScholarshipResult, "Burs Basvuru Sonuclarinin Aciklanmasi", 1, 46, null, SemesterType.Fall),
            Item("fall_scholarship_refund", CalendarEventType.ScholarshipRefund, "Burs Iadelerinin Yapilmasi", 1, 67, null, SemesterType.Fall),

            Item("spring_graduate_app", CalendarEventType.GraduateApplication, "Lisansustu Programlari Basvurulari", 81, -91, null, SemesterType.Spring),
            Item("spring_transfer_app", CalendarEventType.HorizontalTransfer, "Yatay Gecis Basvurulari", 5, -28, null, SemesterType.Spring),
            Item("spring_double_major", CalendarEventType.DoubleMajor, "Cift Anadal ve Yandal Programi Basvurulari", 5, -28, null, SemesterType.Spring),
            Item("spring_tuition", CalendarEventType.TuitionPayment, "Ogrenim Ucretlerinin Odenmesi", 19, -28, null, SemesterType.Spring),
            Item("spring_transfer_result", CalendarEventType.TransferResult, "Yatay Gecis, Cift Anadal ve Yandal Sonuclarinin Ilan Edilmesi", 1, -17, null, SemesterType.Spring),
            Item("spring_transfer_registration", CalendarEventType.TransferRegistration, "Yatay Gecis Kabul Edilen Ogrencilerin Kayitlari (Asil-Yedek)", 5, -14, null, SemesterType.Spring),
            Item("spring_graduate_result", CalendarEventType.GraduateResult, "Lisansustu Program Basvuru Sonuclarinin Ilani", 1, -10, null, SemesterType.Spring),
            Item("spring_course_registration", CalendarEventType.CourseRegistration, "Ders Kayit ve Danisman Onaylari", 5, -7, null, SemesterType.Spring),
            Item("spring_graduate_registration", CalendarEventType.GraduateRegistration, "Lisansustu Programlari Ucret Odeme, Ders Kayitlari ve Danisman Onaylari", 5, -7, null, SemesterType.Spring)
        ];
    }

    private static IEnumerable<CalendarTemplateItemDto> DefaultSummerItems()
    {
        return
        [
            Item("summer_registration", CalendarEventType.SummerSchoolRegistration, "Yaz Ogretimi Ucret Odemeleri ve Ders Kayitlari", 4, -13, null, SemesterType.Summer),
            Item("summer_classes_start", CalendarEventType.ClassesStart, "Yaz Ogretimi Derslerin Baslamasi", 1, 0, null, SemesterType.Summer),
            Item("summer_exam", CalendarEventType.SummerSchoolExam, "Yaz Ogretimi Sinavlari", 5, 42, null, SemesterType.Summer),
            Item("summer_classes_end", CalendarEventType.ClassesEnd, "Yaz Ogretimi Derslerin Sona Ermesi", 1, 46, null, SemesterType.Summer),
            Item("summer_excuse_application", CalendarEventType.ExcuseExamApplication, "Mazeret Sinav Basvurularinin Son Gunu (Yaz Ogretimi)", 1, 49, null, SemesterType.Summer),
            Item("summer_excuse_exam", CalendarEventType.ExcuseExam, "Mazeret Sinavlari (Yaz Ogretimi)", 1, 51, null, SemesterType.Summer),
            Item("summer_publication", CalendarEventType.SummerSchoolGradePublication, "Yaz Ogretimi Sinav Sonuclarinin Ilaninin Son Gunu", 1, 53, null, SemesterType.Summer)
        ];
    }

    private static CalendarTemplateItemDto Item(
        string key,
        CalendarEventType eventType,
        string title,
        int duration,
        int offsetDays,
        string? anchorKey,
        params SemesterType[] semesters)
    {
        return new CalendarTemplateItemDto(
            key,
            eventType,
            title,
            duration,
            CalendarAnchorType.FromTermStart,
            anchorKey,
            offsetDays,
            semesters,
            true,
            SortOrderFromOffset(offsetDays, key),
            true);
    }

    private static int SortOrderFromOffset(int offsetDays, string key)
    {
        var keySeed = Math.Abs(key.GetHashCode(StringComparison.OrdinalIgnoreCase) % 100);
        return (offsetDays + 400) * 100 + keySeed;
    }

    private static IEnumerable<HolidayDefinition> DefaultHolidayDefinitions(DateTime now)
    {
        return
        [
            Fixed("Yilbasi Tatili", "New Year's Day", 1, 1, 1m, now),
            Fixed("Ulusal Egemenlik ve Cocuk Bayrami", "National Sovereignty and Children's Day", 4, 23, 1m, now),
            Fixed("Emek ve Dayanisma Gunu", "Labour and Solidarity Day", 5, 1, 1m, now),
            Fixed("Ataturk'u Anma, Genclik ve Spor Bayrami", "Commemoration of Ataturk, Youth and Sports Day", 5, 19, 1m, now),
            Fixed("Demokrasi ve Milli Birlik Gunu", "Democracy and National Unity Day", 7, 15, 1m, now),
            Fixed("Zafer Bayrami", "Victory Day", 8, 30, 1m, now),
            Fixed("Cumhuriyet Bayrami", "Republic Day", 10, 28, 1.5m, now, isHalfDayStart: true, endDay: 29),
            Important("Ataturk'u Anma Gunu", "Ataturk Memorial Day", 11, 10, now),
            Hijri(
                "Ramazan Bayrami",
                "Eid al-Fitr",
                10,
                1,
                3.5m,
                now,
                [
                    new HolidayOverrideDto(2025, Utc(2025, 3, 29), Utc(2025, 4, 1), false, "Diyanet"),
                    new HolidayOverrideDto(2026, Utc(2026, 3, 19), Utc(2026, 3, 22), false, "Diyanet")
                ]),
            Hijri(
                "Kurban Bayrami",
                "Eid al-Adha",
                12,
                10,
                4.5m,
                now,
                [
                    new HolidayOverrideDto(2025, Utc(2025, 6, 5), Utc(2025, 6, 9), false, "Diyanet"),
                    new HolidayOverrideDto(2026, Utc(2026, 5, 26), Utc(2026, 5, 30), false, "Diyanet")
                ])
        ];
    }

    private static HolidayDefinition Fixed(string name, string englishName, int month, int day, decimal duration, DateTime now, bool isHalfDayStart = false, int? endDay = null) =>
        new()
        {
            Name = name,
            EnglishName = englishName,
            Type = HolidayType.Fixed,
            FixedMonth = month,
            FixedDay = day,
            DurationDays = duration,
            IsHalfDayStart = isHalfDayStart,
            IsOfficialHoliday = true,
            BlocksAcademicEvents = true,
            Source = "2429",
            OverridesJson = endDay.HasValue ? "{}" : "{}",
            IsActive = true,
            CreatedAt = now
        };

    private static HolidayDefinition Important(string name, string englishName, int month, int day, DateTime now) =>
        new()
        {
            Name = name,
            EnglishName = englishName,
            Type = HolidayType.ImportantDay,
            FixedMonth = month,
            FixedDay = day,
            DurationDays = 1m,
            IsHalfDayStart = false,
            IsOfficialHoliday = false,
            BlocksAcademicEvents = true,
            Source = "UniversityPolicy",
            OverridesJson = "{}",
            IsActive = true,
            CreatedAt = now
        };

    private static HolidayDefinition Hijri(string name, string englishName, int month, int day, decimal duration, DateTime now, IReadOnlyList<HolidayOverrideDto> overrides) =>
        new()
        {
            Name = name,
            EnglishName = englishName,
            Type = HolidayType.HijriCalculated,
            HijriMonth = month,
            HijriDay = day,
            DurationDays = duration,
            IsHalfDayStart = true,
            IsOfficialHoliday = true,
            BlocksAcademicEvents = true,
            Source = "Diyanet",
            OverridesJson = SerializeOverrides(overrides),
            IsActive = true,
            CreatedAt = now
        };

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
