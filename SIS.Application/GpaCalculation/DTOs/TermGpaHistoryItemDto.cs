namespace SIS.Application.GpaCalculation.DTOs;

public class TermGpaHistoryItemDto
{
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Semester { get; set; } = string.Empty;
    public int CourseCount { get; set; }
    public int TotalCredits { get; set; }
    public int TotalEcts { get; set; }

    /// <summary>
    /// Dönem ortalaması (ANO). GPA'ya giren ders yoksa (yalnızca S/U/W vb.) <c>null</c> —
    /// gerçek 0 ile "veri yok" ayrımı için. Frontend null'ı "—" olarak gösterir.
    /// </summary>
    public decimal? Sgpa { get; set; }

    /// <summary>Genel not ortalaması (AGNO). GPA'ya giren ders yoksa <c>null</c>.</summary>
    public decimal? Cgpa { get; set; }
}
