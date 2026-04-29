using SIS.Domain.Enums;

namespace SIS.Application.Attendance.DTOs;

public class AttendanceSessionDto
{
    public int Id { get; set; }
    public int CourseSectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int SectionNumber { get; set; }
    public int WeekNumber { get; set; }
    public DateTime SessionDate { get; set; }
    public string Topic { get; set; } = string.Empty;
    public bool IsQrOpen { get; set; }
    public bool IsQrCurrentlyActive { get; set; }
    public DateTime? QrExpiresAt { get; set; }
    public string Token { get; set; } = string.Empty;
    public int TotalStudents { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int LateCount { get; set; }
    public int ExcusedCount { get; set; }
    public List<AttendanceStudentDto> Students { get; set; } = new();
}

public class AttendanceStudentDto
{
    public int StudentId { get; set; }
    public int RegistrationId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public AttendanceStatus? Status { get; set; }
    public AttendanceEntrySource? Source { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

public class CreateAttendanceSessionRequest
{
    public int CourseSectionId { get; set; }
    public int WeekNumber { get; set; }
    public DateTime? SessionDate { get; set; }
    public string? Topic { get; set; }
}

public class SaveAttendanceRequest
{
    public List<AttendanceRecordUpdateDto> Records { get; set; } = new();
}

public class AttendanceRecordUpdateDto
{
    public int StudentId { get; set; }
    public AttendanceStatus Status { get; set; }
}

public class OpenQrAttendanceRequest
{
    public int DurationMinutes { get; set; } = 10;
}

public class QrAttendanceSessionDto
{
    public bool IsActive { get; set; }
    public string Message { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int SectionNumber { get; set; }
    public int WeekNumber { get; set; }
    public DateTime SessionDate { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class SubmitQrAttendanceRequest
{
    public string StudentNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class SubmitQrAttendanceResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
}

public class AttendanceMatrixDto
{
    public int CourseSectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int SectionNumber { get; set; }
    public int TotalWeeks { get; set; }
    public int? CurrentWeekNumber { get; set; }
    public List<AttendanceMatrixWeekDto> Weeks { get; set; } = new();
    public List<AttendanceMatrixStudentDto> Students { get; set; } = new();
}

public class AttendanceMatrixWeekDto
{
    public int WeekNumber { get; set; }
    public int? SessionId { get; set; }
    public DateTime? SessionDate { get; set; }
    public DateTime? SuggestedSessionDate { get; set; }
    public string Topic { get; set; } = string.Empty;
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int LateCount { get; set; }
    public int ExcusedCount { get; set; }
}

public class AttendanceMatrixStudentDto
{
    public int StudentId { get; set; }
    public int RegistrationId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Dictionary<int, AttendanceStudentWeekStatusDto> Weeks { get; set; } = new();
}

public class AttendanceStudentWeekStatusDto
{
    public AttendanceStatus Status { get; set; }
    public AttendanceEntrySource Source { get; set; }
    public DateTime? SubmittedAt { get; set; }
}
