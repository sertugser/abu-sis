using SIS.Application.CalendarGenerator.DTOs;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.CalendarGenerator;

public partial class CalendarGeneratorService
{
    private static void ValidateChronology(IReadOnlyList<GeneratedCalendarEventDto> events, List<CalendarWarningDto> warnings)
    {
        foreach (var group in events.Where(e => !e.IsHoliday).GroupBy(e => e.Semester))
        {
            var list = group.ToList();
            CheckOrder(list, group.Key, CalendarEventType.CourseRegistration, CalendarEventType.ClassesStart, warnings);
            CheckOrder(list, group.Key, CalendarEventType.ClassesStart, CalendarEventType.AddDrop, warnings);
            CheckOrder(list, group.Key, CalendarEventType.AddDrop, CalendarEventType.MidtermExam, warnings);
            CheckOrder(list, group.Key, CalendarEventType.MidtermExam, CalendarEventType.ExcuseExamApplication, warnings, secondKeyContains: "midterm_excuse_application");
            CheckOrder(list, group.Key, CalendarEventType.ExcuseExamApplication, CalendarEventType.ExcuseExam, warnings, firstKeyContains: "midterm_excuse_application", secondKeyContains: "midterm_excuse_exam");
            CheckOrder(list, group.Key, CalendarEventType.ExcuseExam, CalendarEventType.MidtermGradePublication, warnings, firstKeyContains: "midterm_excuse_exam", secondKeyContains: "midterm_publication");
            CheckOrder(list, group.Key, CalendarEventType.MidtermGradePublication, CalendarEventType.FinalExam, warnings, firstKeyContains: "midterm_publication", secondKeyContains: "final_exam");
            CheckOrder(list, group.Key, CalendarEventType.ClassesEnd, CalendarEventType.FinalExam, warnings);
            CheckOrder(list, group.Key, CalendarEventType.FinalExam, CalendarEventType.FinalGradePublication, warnings);
            CheckOrder(list, group.Key, CalendarEventType.FinalGradePublication, CalendarEventType.MakeupExam, warnings, secondKeyContains: "makeup_exam");
            CheckOrder(list, group.Key, CalendarEventType.MakeupExam, CalendarEventType.MakeupGradePublication, warnings, firstKeyContains: "makeup_exam");

            var withdrawal = First(list, CalendarEventType.CourseWithdrawal);
            var midterm = FirstByTemplateKey(list, "midterm_exam") ?? First(list, CalendarEventType.MidtermExam);
            if (withdrawal != null && midterm != null && withdrawal.StartDate.Date < midterm.StartDate.Date)
            {
                warnings.Add(new(
                    "CHRONOLOGY",
                    "Error",
                    $"{SemesterLabel(group.Key)}: {withdrawal.Title} etkinliği {midterm.Title} başlamadan önce olamaz.",
                    withdrawal.DraftEventId));
            }
        }
    }

    private static void CheckOrder(IReadOnlyList<GeneratedCalendarEventDto> events, SemesterType semester, CalendarEventType first, CalendarEventType second, List<CalendarWarningDto> warnings, string? firstKeyContains = null, string? secondKeyContains = null)
    {
        var firstEvent = events.Where(e => e.EventType == first && (firstKeyContains == null || (e.TemplateItemKey?.Contains(firstKeyContains, StringComparison.OrdinalIgnoreCase) ?? false)))
            .OrderBy(e => e.StartDate).LastOrDefault();
        var secondEvent = events.Where(e => e.EventType == second && (secondKeyContains == null || (e.TemplateItemKey?.Contains(secondKeyContains, StringComparison.OrdinalIgnoreCase) ?? false)))
            .OrderBy(e => e.StartDate).FirstOrDefault();
        if (firstEvent == null || secondEvent == null) return;

        if ((firstEvent.EndDate ?? firstEvent.StartDate).Date > secondEvent.StartDate.Date)
            warnings.Add(new("CHRONOLOGY", "Error", $"{SemesterLabel(semester)}: {firstEvent.Title} etkinliği {secondEvent.Title} etkinliğinden sonra bitiyor.", secondEvent.DraftEventId));
    }

    private static IEnumerable<CalendarWarningDto> ValidateRegulationRules(IReadOnlyList<GeneratedCalendarEventDto> events, IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        foreach (var group in events.Where(e => !e.IsHoliday).GroupBy(e => e.Semester))
        {
            var list = group.ToList();
            foreach (var warning in group.Key == SemesterType.Summer
                ? ValidateSummerRegulation(list, holidays)
                : ValidateRegularSemesterRegulation(group.Key, list, holidays))
            {
                yield return warning;
            }
        }
    }

    private static IEnumerable<CalendarWarningDto> ValidateRegularSemesterRegulation(SemesterType semester, IReadOnlyList<GeneratedCalendarEventDto> events, IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        var classesStart = First(events, CalendarEventType.ClassesStart);
        var classesEnd = First(events, CalendarEventType.ClassesEnd);
        var finalExam = FirstByTemplateKey(events, "final_exam") ?? First(events, CalendarEventType.FinalExam);

        if (classesStart == null || classesEnd == null || finalExam == null)
        {
            yield return new CalendarWarningDto("REG_REQUIRED_EVENT", "Error", $"{SemesterLabel(semester)}: Yönetmelik kontrolü için ders başlangıcı, ders bitişi ve final sınavı bulunmalıdır.");
            yield break;
        }

        var classesEndDate = classesEnd.EndDate ?? classesEnd.StartDate;
        var teachingPeriodDays = InclusiveDays(classesStart.StartDate, classesEndDate);
        if (teachingPeriodDays < RegularTeachingPeriodDays)
        {
            yield return new CalendarWarningDto(
                "REG_MIN_TEACHING_WEEKS",
                "Error",
                $"{SemesterLabel(semester)}: Madde 21 gereği kayıt ve sınavlar hariç ders süresi en az 14 hafta olmalıdır ({teachingPeriodDays} gün).",
                classesEnd.DraftEventId);
        }

        var semesterWithExamsDays = InclusiveDays(classesStart.StartDate, finalExam.EndDate ?? finalExam.StartDate);
        if (semesterWithExamsDays < RegularSemesterWithExamsDays)
        {
            yield return new CalendarWarningDto(
                "REG_MIN_SEMESTER_WEEKS",
                "Error",
                $"{SemesterLabel(semester)}: Madde 7 gereği yarıyıl sınavlar dahil en az 16 hafta olmalıdır ({semesterWithExamsDays} gün).",
                finalExam.DraftEventId);
        }

        foreach (var warning in ValidatePublicationDeadline(events, holidays, CalendarEventType.ExcuseExam, "midterm_excuse_exam", CalendarEventType.MidtermGradePublication, "midterm_publication", semester))
            yield return warning;
        foreach (var warning in ValidatePublicationDeadline(events, holidays, CalendarEventType.FinalExam, "final_exam", CalendarEventType.FinalGradePublication, "final_publication", semester))
            yield return warning;
        foreach (var warning in ValidatePublicationDeadline(events, holidays, CalendarEventType.MakeupExam, "makeup_exam", CalendarEventType.MakeupGradePublication, "makeup_publication", semester))
            yield return warning;
    }

    private static IEnumerable<CalendarWarningDto> ValidateSummerRegulation(IReadOnlyList<GeneratedCalendarEventDto> events, IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        var classesStart = First(events, CalendarEventType.ClassesStart);
        var classesEnd = First(events, CalendarEventType.ClassesEnd);
        var summerExam = First(events, CalendarEventType.SummerSchoolExam);

        if (classesStart == null || classesEnd == null || summerExam == null)
        {
            yield return new CalendarWarningDto("REG_SUMMER_REQUIRED_EVENT", "Error", "Yaz: Yönetmelik kontrolü için ders başlangıcı, ders bitişi ve yaz okulu sınavı bulunmalıdır.");
            yield break;
        }

        var classesEndDate = classesEnd.EndDate ?? classesEnd.StartDate;
        var teachingWeekdays = CountTeachingWeekdays(classesStart.StartDate, classesEndDate, holidays);
        if (teachingWeekdays < SummerTeachingWeekdays)
        {
            yield return new CalendarWarningDto(
                "REG_SUMMER_TEACHING_WEEKS",
                "Error",
                $"Yaz: Madde 22 gereği yaz okulu sınavları hariç 7 haftalık ders süresini kapsamalıdır ({teachingWeekdays} hafta içi gün).",
                classesEnd.DraftEventId);
        }

        if (summerExam.StartDate.Date <= classesEndDate.Date)
        {
            yield return new CalendarWarningDto(
                "REG_SUMMER_EXAM_ORDER",
                "Error",
                "Yaz: Madde 22 gereği yaz okulu sınavları ders süresi tamamlandıktan sonra başlamalıdır.",
                summerExam.DraftEventId);
        }

        foreach (var warning in ValidatePublicationDeadline(events, holidays, CalendarEventType.SummerSchoolExam, "summer_exam", CalendarEventType.SummerSchoolGradePublication, "summer_publication", SemesterType.Summer))
            yield return warning;
    }

    private static IEnumerable<CalendarWarningDto> ValidatePublicationDeadline(
        IReadOnlyList<GeneratedCalendarEventDto> events,
        IReadOnlyList<ResolvedHolidayDto> holidays,
        CalendarEventType examType,
        string examKey,
        CalendarEventType publicationType,
        string publicationKey,
        SemesterType semester)
    {
        var exam = FirstByTemplateKey(events, examKey) ?? First(events, examType);
        var publication = FirstByTemplateKey(events, publicationKey) ?? First(events, publicationType);
        if (exam == null || publication == null)
            yield break;

        var examEnd = exam.EndDate ?? exam.StartDate;
        if (publication.StartDate.Date < examEnd.Date)
            yield break;

        var businessDays = CountBusinessDaysAfter(examEnd, publication.StartDate, holidays);
        if (businessDays > GradePublicationBusinessDayLimit)
        {
            yield return new CalendarWarningDto(
                "REG_GRADE_PUBLICATION",
                "Error",
                $"{SemesterLabel(semester)}: Madde 36 gereği {publication.Title} sınav dönemini izleyen en geç 10 iş günü içinde ilan edilmelidir ({businessDays} iş günü).",
                publication.DraftEventId);
        }
    }
}
