using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.DTOs;
using SIS.Application.Common.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AcademicCalendarService : IAcademicCalendarService
{
    private readonly AppDbContext _context;

    public AcademicCalendarService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ActiveTermCalendarDto?> GetActiveTermCalendarAsync()
    {
        var term = await _context.AcademicTerms
            .Include(t => t.CalendarEvents.Where(e => e.IsPublic))
            .FirstOrDefaultAsync(t => t.IsActive);

        if (term is null)
            return null;

        var events = term.CalendarEvents
            .OrderBy(e => e.StartDate)
            .Select(e => new PublicCalendarEventDto(
                e.Id,
                e.EventType.ToString(),
                e.EventType switch
                {
                    CalendarEventType.CourseRegistration => "Ders Kayıt",
                    CalendarEventType.NewStudentRegistration => "Yeni Öğrenci Ders Kayıt",
                    CalendarEventType.AdvisorApproval => "Danışman Onayı",
                    CalendarEventType.AddDrop => "Ders Ekle/Bırak",
                    CalendarEventType.AddDropApproval => "Ders Ekle/Bırak Onayı",
                    CalendarEventType.CourseWithdrawal => "Ders Çekilme",
                    CalendarEventType.AdvisorWithdrawal => "Danışman Dersten Çekme",
                    CalendarEventType.MidtermExam => "Ara Sınav",
                    CalendarEventType.MidtermGradeEntry => "Ara Sınav Not Girişi",
                    CalendarEventType.MidtermGradePublication => "Ara Sınav Not Yayımlama",
                    CalendarEventType.FinalExam => "Dönem Sonu Sınavı",
                    CalendarEventType.FinalGradeEntry => "Final Not Girişi",
                    CalendarEventType.FinalGradePublication => "Final Not Yayımlama",
                    CalendarEventType.MakeupExam => "Bütünleme Sınavı",
                    CalendarEventType.MakeupGradeEntry => "Bütünleme Not Girişi",
                    CalendarEventType.MakeupGradePublication => "Bütünleme Not Yayımlama",
                    CalendarEventType.NationalHoliday => "Milli Tatil",
                    CalendarEventType.ReligiousHoliday => "Dini Tatil",
                    CalendarEventType.ClassesStart => "Derslerin Başlangıcı",
                    CalendarEventType.ClassesEnd => "Derslerin Bitişi",
                    CalendarEventType.TuitionPayment => "Harç Ödeme",
                    CalendarEventType.GraduateApplication => "Lisansüstü Başvuru",
                    CalendarEventType.HorizontalTransfer => "Yatay Geçiş",
                    CalendarEventType.DoubleMajor => "Çift Anadal/Yan Dal",
                    CalendarEventType.Other => "Diğer",
                    _ => e.EventType.ToString()
                },
                e.Title,
                e.StartDate,
                e.EndDate,
                e.Note
            ))
            .ToList();

        return new ActiveTermCalendarDto(
            term.Id,
            term.Name,
            term.ShortName,
            term.EnglishName,
            events
        );
    }
}
