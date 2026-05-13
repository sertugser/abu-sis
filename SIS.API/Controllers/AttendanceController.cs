using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.API.Security;
using SIS.Application.Attendance.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/attendance")]
public class AttendanceController : ControllerBase
{
    private static readonly RegistrationStatus[] IncludedRegistrationStatuses =
    [
        RegistrationStatus.Active,
        RegistrationStatus.Completed,
        RegistrationStatus.PendingAdvisorApproval,
        RegistrationStatus.Withdrawn
    ];

    private readonly AppDbContext _context;

    public AttendanceController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("sessions")]
    [Authorize]
    [RequirePermission("menu.yoklama-girisi.view")]
    public async Task<IActionResult> CreateOrGetSession([FromBody] CreateAttendanceSessionRequest request)
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return NotFound(new { error = "Ogretim elemani bilgisi bulunamadi." });

        var section = await _context.CourseSections
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.CourseSectionId && s.InstructorId == instructor.Id);

        if (section == null)
            return NotFound(new { error = "Bu sube sizin dersleriniz arasinda bulunamadi." });

        var weekNumber = NormalizeWeekNumber(request.WeekNumber);
        var sessionDate = NormalizeSessionDate(request.SessionDate);
        var session = await _context.AttendanceSessions
            .FirstOrDefaultAsync(s => s.CourseSectionId == request.CourseSectionId && s.WeekNumber == weekNumber);

        if (session == null)
        {
            session = new AttendanceSession
            {
                CourseSectionId = request.CourseSectionId,
                InstructorId = instructor.Id,
                WeekNumber = weekNumber,
                SessionDate = sessionDate,
                Topic = NormalizeTopic(request.Topic),
                CreatedAt = DateTime.UtcNow,
            };
            _context.AttendanceSessions.Add(session);
            await _context.SaveChangesAsync();
        }
        else if ((!string.IsNullOrWhiteSpace(request.Topic) && session.Topic != request.Topic.Trim())
            || session.SessionDate != sessionDate)
        {
            session.Topic = NormalizeTopic(request.Topic);
            session.SessionDate = sessionDate;
            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return Ok(await BuildSessionDtoAsync(session.Id));
    }

    [HttpGet("sessions/{id:int}")]
    [Authorize]
    [RequirePermission("menu.yoklama-girisi.view")]
    public async Task<IActionResult> GetSession(int id)
    {
        var owned = await FindOwnedSessionAsync(id);
        if (owned == null)
            return NotFound(new { error = "Yoklama oturumu bulunamadi." });

        return Ok(await BuildSessionDtoAsync(id));
    }

    [HttpGet("sections/{sectionId:int}/matrix")]
    [Authorize]
    [RequirePermission("menu.yoklama-girisi.view")]
    public async Task<IActionResult> GetSectionMatrix(int sectionId)
    {
        var section = await FindOwnedSectionAsync(sectionId);
        if (section == null)
            return NotFound(new { error = "Bu sube sizin dersleriniz arasinda bulunamadi." });

        return Ok(await BuildMatrixDtoAsync(sectionId));
    }

    [HttpPut("sessions/{id:int}/records")]
    [Authorize]
    [RequirePermission("menu.yoklama-girisi.view")]
    public async Task<IActionResult> SaveRecords(int id, [FromBody] SaveAttendanceRequest request)
    {
        var session = await FindOwnedSessionAsync(id);
        if (session == null)
            return NotFound(new { error = "Yoklama oturumu bulunamadi." });

        var registrations = await _context.CourseRegistrations
            .Where(r => r.CourseSectionId == session.CourseSectionId
                && IncludedRegistrationStatuses.Contains(r.Status))
            .ToDictionaryAsync(r => r.StudentId);

        var existingRecords = await _context.AttendanceRecords
            .Where(r => r.AttendanceSessionId == session.Id)
            .ToDictionaryAsync(r => r.StudentId);

        var now = DateTime.UtcNow;
        foreach (var update in request.Records.GroupBy(r => r.StudentId).Select(g => g.Last()))
        {
            if (!registrations.TryGetValue(update.StudentId, out var registration))
            {
                continue;
            }

            if (existingRecords.TryGetValue(update.StudentId, out var record))
            {
                record.Status = update.Status;
                record.Source = AttendanceEntrySource.Manual;
                record.UpdatedAt = now;
            }
            else
            {
                _context.AttendanceRecords.Add(new AttendanceRecord
                {
                    AttendanceSessionId = session.Id,
                    CourseRegistrationId = registration.Id,
                    StudentId = update.StudentId,
                    Status = update.Status,
                    Source = AttendanceEntrySource.Manual,
                    CreatedAt = now,
                });
            }
        }

        session.UpdatedAt = now;
        await _context.SaveChangesAsync();

        return Ok(await BuildSessionDtoAsync(session.Id));
    }

    [HttpPost("sessions/{id:int}/qr/open")]
    [Authorize]
    [RequirePermission("menu.yoklama-girisi.view")]
    public async Task<IActionResult> OpenQr(int id, [FromBody] OpenQrAttendanceRequest request)
    {
        var session = await FindOwnedSessionAsync(id);
        if (session == null)
            return NotFound(new { error = "Yoklama oturumu bulunamadi." });

        var now = DateTime.UtcNow;
        var minutes = Math.Clamp(request.DurationMinutes, 1, 1440);
        var previousQrSessions = await _context.AttendanceSessions
            .Where(other => other.Id != session.Id
                && other.CourseSectionId == session.CourseSectionId
                && other.WeekNumber == session.WeekNumber
                && other.IsQrOpen)
            .ToListAsync();

        foreach (var previousQrSession in previousQrSessions)
        {
            previousQrSession.IsQrOpen = false;
            previousQrSession.QrExpiresAt = now;
            previousQrSession.UpdatedAt = now;
        }

        session.Token = Guid.NewGuid().ToString("N");
        session.IsQrOpen = true;
        session.QrExpiresAt = now.AddMinutes(minutes);
        session.UpdatedAt = now;
        await _context.SaveChangesAsync();

        return Ok(await BuildSessionDtoAsync(session.Id));
    }

    [HttpPost("sessions/{id:int}/qr/close")]
    [Authorize]
    [RequirePermission("menu.yoklama-girisi.view")]
    public async Task<IActionResult> CloseQr(int id)
    {
        var session = await FindOwnedSessionAsync(id);
        if (session == null)
            return NotFound(new { error = "Yoklama oturumu bulunamadi." });

        session.IsQrOpen = false;
        session.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(await BuildSessionDtoAsync(session.Id));
    }

    [HttpGet("qr/{token}")]
    [AllowAnonymous]
    [EnableRateLimiting("PublicAttendanceQr")]
    [PublicEndpointJustification("QR attendance links are opened by students without an existing session; token expiry and rate limiting constrain use.")]
    public async Task<IActionResult> GetQrSession(string token)
    {
        var session = await _context.AttendanceSessions
            .AsNoTracking()
            .Include(s => s.CourseSection)
                .ThenInclude(cs => cs.Course)
            .FirstOrDefaultAsync(s => s.Token == token);

        if (session == null)
            return NotFound(new { error = "Yoklama formu bulunamadi." });

        var isActive = IsQrActive(session);
        return Ok(new QrAttendanceSessionDto
        {
            IsActive = isActive,
            Message = isActive ? "Yoklama formu aktif." : "Yoklama formu kapali veya suresi dolmus.",
            CourseCode = session.CourseSection.Course.Code,
            CourseName = session.CourseSection.Course.Name,
            SectionNumber = session.CourseSection.SectionNumber,
            WeekNumber = session.WeekNumber,
            SessionDate = session.SessionDate,
            ExpiresAt = session.QrExpiresAt
        });
    }

    [HttpPost("qr/{token}/submit")]
    [Authorize]
    [RequirePermission("menu.devamsizlik-durumu.view")]
    [EnableRateLimiting("PublicAttendanceQr")]
    public async Task<IActionResult> SubmitQr(string token, [FromBody] SubmitQrAttendanceRequest? request = null)
    {
        var session = await _context.AttendanceSessions
            .Include(s => s.CourseSection)
                .ThenInclude(cs => cs.Course)
            .FirstOrDefaultAsync(s => s.Token == token);

        if (session == null)
            return NotFound(new SubmitQrAttendanceResponse { Message = "Yoklama formu bulunamadi." });

        if (!IsQrActive(session))
            return BadRequest(new SubmitQrAttendanceResponse { Message = "Yoklama formu kapali veya suresi dolmus." });

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (student == null)
            return Forbid();

        var registration = await _context.CourseRegistrations
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.CourseSectionId == session.CourseSectionId
                && r.StudentId == student.Id
                && IncludedRegistrationStatuses.Contains(r.Status));

        if (registration == null)
            return BadRequest(new SubmitQrAttendanceResponse { Message = "Bu subeye ait ogrenci kaydi bulunamadi." });

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == registration.Student.UserId);

        var expectedFirstName = user?.FirstName ?? string.Empty;
        var expectedLastName = user?.LastName ?? string.Empty;

        var now = DateTime.UtcNow;
        var record = await _context.AttendanceRecords
            .FirstOrDefaultAsync(r => r.AttendanceSessionId == session.Id && r.StudentId == registration.StudentId);

        if (record == null)
        {
            _context.AttendanceRecords.Add(new AttendanceRecord
            {
                AttendanceSessionId = session.Id,
                CourseRegistrationId = registration.Id,
                StudentId = registration.StudentId,
                Status = AttendanceStatus.Present,
                Source = AttendanceEntrySource.Qr,
                CreatedAt = now,
                QrSubmittedAt = now
            });
        }
        else
        {
            record.Status = AttendanceStatus.Present;
            record.Source = AttendanceEntrySource.Qr;
            record.UpdatedAt = now;
            record.QrSubmittedAt = now;
        }

        await _context.SaveChangesAsync();

        return Ok(new SubmitQrAttendanceResponse
        {
            Success = true,
            Message = "Yoklama kaydiniz alindi.",
            StudentName = $"{expectedFirstName} {expectedLastName}".Trim()
        });
    }

    private async Task<Instructor?> GetInstructorAsync()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return null;

        return await _context.Instructors.FirstOrDefaultAsync(i => i.UserId == userId);
    }

    private async Task<AttendanceSession?> FindOwnedSessionAsync(int id)
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return null;

        return await _context.AttendanceSessions
            .FirstOrDefaultAsync(s => s.Id == id && s.InstructorId == instructor.Id);
    }

    private async Task<CourseSection?> FindOwnedSectionAsync(int sectionId)
    {
        var instructor = await GetInstructorAsync();
        if (instructor == null)
            return null;

        return await _context.CourseSections
            .AsNoTracking()
            .Include(section => section.Course)
            .FirstOrDefaultAsync(section => section.Id == sectionId && section.InstructorId == instructor.Id);
    }

    private async Task<AttendanceSessionDto> BuildSessionDtoAsync(int sessionId)
    {
        var session = await _context.AttendanceSessions
            .AsNoTracking()
            .Include(s => s.CourseSection)
                .ThenInclude(cs => cs.Course)
            .FirstAsync(s => s.Id == sessionId);

        var registrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(r => r.Student)
            .Where(r => r.CourseSectionId == session.CourseSectionId
                && IncludedRegistrationStatuses.Contains(r.Status))
            .ToListAsync();

        var studentUserIds = registrations.Select(r => r.Student.UserId).Distinct().ToList();
        var userMap = await _context.Users
            .AsNoTracking()
            .Where(u => studentUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var records = await _context.AttendanceRecords
            .AsNoTracking()
            .Where(r => r.AttendanceSessionId == session.Id)
            .ToListAsync();
        var recordMap = records.ToDictionary(r => r.StudentId);

        var students = registrations
            .Select(registration =>
            {
                var user = userMap.GetValueOrDefault(registration.Student.UserId);
                recordMap.TryGetValue(registration.StudentId, out var record);

                return new AttendanceStudentDto
                {
                    StudentId = registration.StudentId,
                    RegistrationId = registration.Id,
                    StudentNumber = registration.Student.StudentNumber,
                    FirstName = user?.FirstName ?? string.Empty,
                    LastName = user?.LastName ?? string.Empty,
                    Email = user?.Email ?? string.Empty,
                    Status = record?.Status,
                    Source = record?.Source,
                    SubmittedAt = record?.QrSubmittedAt ?? record?.UpdatedAt ?? record?.CreatedAt
                };
            })
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ToList();

        return new AttendanceSessionDto
        {
            Id = session.Id,
            CourseSectionId = session.CourseSectionId,
            CourseCode = session.CourseSection.Course.Code,
            CourseName = session.CourseSection.Course.Name,
            SectionNumber = session.CourseSection.SectionNumber,
            WeekNumber = session.WeekNumber,
            SessionDate = session.SessionDate,
            Topic = session.Topic ?? string.Empty,
            IsQrOpen = session.IsQrOpen,
            IsQrCurrentlyActive = IsQrActive(session),
            QrExpiresAt = session.QrExpiresAt,
            Token = session.Token,
            TotalStudents = students.Count,
            PresentCount = records.Count(r => r.Status == AttendanceStatus.Present),
            AbsentCount = records.Count(r => r.Status == AttendanceStatus.Absent),
            LateCount = records.Count(r => r.Status == AttendanceStatus.Late),
            ExcusedCount = records.Count(r => r.Status == AttendanceStatus.Excused),
            Students = students
        };
    }

    private async Task<AttendanceMatrixDto> BuildMatrixDtoAsync(int sectionId)
    {
        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(s => s.Course)
            .Include(s => s.Term)
            .FirstAsync(s => s.Id == sectionId);

        var registrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(r => r.Student)
            .Where(r => r.CourseSectionId == sectionId
                && IncludedRegistrationStatuses.Contains(r.Status))
            .ToListAsync();

        var studentUserIds = registrations.Select(r => r.Student.UserId).Distinct().ToList();
        var userMap = await _context.Users
            .AsNoTracking()
            .Where(u => studentUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var sessions = await _context.AttendanceSessions
            .AsNoTracking()
            .Where(s => s.CourseSectionId == sectionId)
            .OrderBy(s => s.WeekNumber)
            .ToListAsync();
        var sessionIds = sessions.Select(s => s.Id).ToList();

        var records = sessionIds.Count == 0
            ? new List<AttendanceRecord>()
            : await _context.AttendanceRecords
                .AsNoTracking()
                .Where(r => sessionIds.Contains(r.AttendanceSessionId))
                .ToListAsync();

        var sessionsById = sessions.ToDictionary(s => s.Id);
        var recordsByWeek = records
            .Where(record => sessionsById.ContainsKey(record.AttendanceSessionId))
            .GroupBy(record => sessionsById[record.AttendanceSessionId].WeekNumber)
            .ToDictionary(group => group.Key, group => group.ToList());
        var recordByStudentAndWeek = records
            .Where(record => sessionsById.ContainsKey(record.AttendanceSessionId))
            .ToDictionary(
                record => (record.StudentId, sessionsById[record.AttendanceSessionId].WeekNumber),
                record => record);

        var totalWeeks = await GetAttendanceWeekCountAsync();
        var maxSessionWeek = sessions.Count == 0 ? 0 : sessions.Max(session => session.WeekNumber);
        var weekCount = Math.Max(totalWeeks, maxSessionWeek);
        var sessionsByWeek = sessions
            .GroupBy(session => session.WeekNumber)
            .ToDictionary(group => group.Key, group => group.First());
        var scheduledSlotRows = await _context.SectionSchedules
            .AsNoTracking()
            .Where(schedule => schedule.CourseSectionId == sectionId)
            .Select(schedule => new
            {
                schedule.TimeSlot.DayOfWeek,
                schedule.TimeSlot.StartTime,
                schedule.TimeSlot.SlotOrder
            })
            .ToListAsync();
        var scheduledSlots = scheduledSlotRows
            .Select(slot => new SectionScheduleSlot(slot.DayOfWeek, slot.StartTime, slot.SlotOrder))
            .ToList();

        return new AttendanceMatrixDto
        {
            CourseSectionId = section.Id,
            CourseCode = section.Course.Code,
            CourseName = section.Course.Name,
            SectionNumber = section.SectionNumber,
            TotalWeeks = weekCount,
            CurrentWeekNumber = CalculateCurrentWeekNumber(section.Term, weekCount),
            Weeks = Enumerable.Range(1, weekCount)
                .Select(weekNumber =>
                {
                    sessionsByWeek.TryGetValue(weekNumber, out var session);
                    var weekRecords = recordsByWeek.GetValueOrDefault(weekNumber) ?? [];

                    return new AttendanceMatrixWeekDto
                    {
                        WeekNumber = weekNumber,
                        SessionId = session?.Id,
                        SessionDate = session?.SessionDate,
                        SuggestedSessionDate = CalculateSuggestedSessionDate(section.Term, weekNumber, scheduledSlots),
                        Topic = session?.Topic ?? string.Empty,
                        PresentCount = weekRecords.Count(record => record.Status == AttendanceStatus.Present),
                        AbsentCount = weekRecords.Count(record => record.Status == AttendanceStatus.Absent),
                        LateCount = weekRecords.Count(record => record.Status == AttendanceStatus.Late),
                        ExcusedCount = weekRecords.Count(record => record.Status == AttendanceStatus.Excused)
                    };
                })
                .ToList(),
            Students = registrations
                .Select(registration =>
                {
                    var user = userMap.GetValueOrDefault(registration.Student.UserId);
                    var weeks = Enumerable.Range(1, weekCount)
                        .Select(weekNumber =>
                        {
                            recordByStudentAndWeek.TryGetValue((registration.StudentId, weekNumber), out var record);
                            return new { weekNumber, record };
                        })
                        .Where(item => item.record != null)
                        .ToDictionary(
                            item => item.weekNumber,
                            item => new AttendanceStudentWeekStatusDto
                            {
                                Status = item.record!.Status,
                                Source = item.record.Source,
                                SubmittedAt = item.record.QrSubmittedAt ?? item.record.UpdatedAt ?? item.record.CreatedAt
                            });

                    return new AttendanceMatrixStudentDto
                    {
                        StudentId = registration.StudentId,
                        RegistrationId = registration.Id,
                        StudentNumber = registration.Student.StudentNumber,
                        FirstName = user?.FirstName ?? string.Empty,
                        LastName = user?.LastName ?? string.Empty,
                        Email = user?.Email ?? string.Empty,
                        Weeks = weeks
                    };
                })
                .OrderBy(student => student.LastName)
                .ThenBy(student => student.FirstName)
                .ToList()
        };
    }

    private static DateTime NormalizeSessionDate(DateTime? sessionDate)
    {
        var date = (sessionDate ?? DateTime.UtcNow).Date;
        return DateTime.SpecifyKind(date, DateTimeKind.Utc);
    }

    private static int NormalizeWeekNumber(int weekNumber) =>
        Math.Clamp(weekNumber <= 0 ? 1 : weekNumber, 1, 52);

    private static int? CalculateCurrentWeekNumber(AcademicTerm term, int weekCount)
    {
        var today = DateTime.UtcNow.Date;
        var start = DateTime.SpecifyKind(term.EffectiveStart, DateTimeKind.Utc).Date;
        var end = DateTime.SpecifyKind(term.EffectiveEnd, DateTimeKind.Utc).Date;

        if (today < start || today > end)
            return null;

        var weekNumber = ((today - start).Days / 7) + 1;
        return Math.Clamp(weekNumber, 1, Math.Max(1, weekCount));
    }

    private static DateTime? CalculateSuggestedSessionDate(
        AcademicTerm term,
        int weekNumber,
        IReadOnlyCollection<SectionScheduleSlot> scheduledSlots)
    {
        if (scheduledSlots.Count == 0)
            return null;

        var weekStart = DateTime.SpecifyKind(term.EffectiveStart, DateTimeKind.Utc).Date
            .AddDays((Math.Max(1, weekNumber) - 1) * 7);
        var selectedSlot = scheduledSlots
            .Select(slot => new
            {
                Slot = slot,
                DayOffset = DaysUntil(weekStart.DayOfWeek, slot.DayOfWeek)
            })
            .OrderBy(item => item.DayOffset)
            .ThenBy(item => item.Slot.StartTime)
            .ThenBy(item => item.Slot.SlotOrder)
            .First();

        return DateTime.SpecifyKind(weekStart.AddDays(selectedSlot.DayOffset), DateTimeKind.Utc);
    }

    private static int DaysUntil(DayOfWeek from, DayOfWeek to) =>
        ((int)to - (int)from + 7) % 7;

    private async Task<int> GetAttendanceWeekCountAsync()
    {
        var value = await _context.SystemConfigs
            .AsNoTracking()
            .Where(config => config.Key == "Attendance.TermWeekCount")
            .Select(config => config.Value)
            .FirstOrDefaultAsync();

        return int.TryParse(value, out var weekCount)
            ? Math.Clamp(weekCount, 1, 52)
            : 14;
    }

    private static string? NormalizeTopic(string? topic)
    {
        var value = topic?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Length > 200 ? value[..200] : value;
    }

    private static bool IsQrActive(AttendanceSession session) =>
        session.IsQrOpen && session.QrExpiresAt.HasValue && session.QrExpiresAt.Value > DateTime.UtcNow;

    private static bool NamesMatch(string expected, string actual) =>
        NormalizeName(expected) == NormalizeName(actual);

    private static string NormalizeName(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(ch))
                builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed record SectionScheduleSlot(DayOfWeek DayOfWeek, TimeOnly StartTime, int SlotOrder);
}
