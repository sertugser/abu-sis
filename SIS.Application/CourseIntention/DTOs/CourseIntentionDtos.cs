using SIS.Domain.Enums;

namespace SIS.Application.CourseIntention.DTOs;

public class CourseIntentionSurveyDto
{
    public int Id { get; set; }
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public CourseIntentionSurveyStatus Status { get; set; }
    public DateTime OpenAtUtc { get; set; }
    public DateTime CloseAtUtc { get; set; }
    public bool IsManualOverride { get; set; }
    public DateTime? LastStatusChangedAtUtc { get; set; }
}

public class CourseIntentionOptionDto
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Ects { get; set; }
    public int SuggestedSemester { get; set; }
    public bool IsCompulsory { get; set; }
    public string TakeStatus { get; set; } = string.Empty;
    public string? PreviousGrade { get; set; }
    public bool IsBlockedByAdvisor { get; set; }
    public bool IsLocked { get; set; }
    public CourseIntentionLevel? LockedIntention { get; set; }
    public string? LockReason { get; set; }
}

public class CourseIntentionResponseDto
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public CourseIntentionLevel Intention { get; set; }
}

public class StudentSurveyPreferenceDto
{
    public string PreferredDays { get; set; } = string.Empty;
    public string PreferredTimeRanges { get; set; } = string.Empty;
}

public class CourseIntentionSurveyDetailDto
{
    public CourseIntentionSurveyDto Survey { get; set; } = new();
    public List<CourseIntentionOptionDto> AvailableCourses { get; set; } = new();
    public List<CourseIntentionResponseDto> Responses { get; set; } = new();
    public StudentSurveyPreferenceDto Preference { get; set; } = new();
}

public class SaveCourseIntentionsRequest
{
    public int SurveyId { get; set; }
    public List<SaveCourseIntentionItemDto> Intentions { get; set; } = new();
    public StudentSurveyPreferenceDto Preference { get; set; } = new();
}

public class SaveCourseIntentionItemDto
{
    public int CourseId { get; set; }
    public CourseIntentionLevel Intention { get; set; }
}

public class CourseIntentionSummaryDto
{
    public int SurveyId { get; set; }
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public int TotalRespondingStudents { get; set; }
    public List<CourseIntentionSummaryItemDto> Courses { get; set; } = new();
}

public class CourseIntentionSummaryItemDto
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int CertainCount { get; set; }
    public int MaybeCount { get; set; }
    public List<string> PreferredDays { get; set; } = new();
    public List<string> PreferredTimeRanges { get; set; } = new();
}

public class UpsertCourseIntentionSurveyRequest
{
    public int TermId { get; set; }
    public string? Title { get; set; }
    public DateTime OpenAtUtc { get; set; }
    public DateTime CloseAtUtc { get; set; }
}

public class SetCourseIntentionSurveyStateRequest
{
    public CourseIntentionSurveyStatus Status { get; set; }
    public bool IsManualOverride { get; set; } = true;
}
