using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class TermCalendarEvent
{
    public int Id { get; set; }
    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;
    public CalendarEventType EventType { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsPublic { get; set; } = true;
    public string? Note { get; set; }
    public bool IsGenerated { get; set; }
    public string? ChainCode { get; set; }
    public string? TemplateItemKey { get; set; }
    public int? GeneratedDraftId { get; set; }
    public bool BlocksAcademicEvents { get; set; }
}
