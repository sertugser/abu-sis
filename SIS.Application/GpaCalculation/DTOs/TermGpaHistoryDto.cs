namespace SIS.Application.GpaCalculation.DTOs;

/// <summary>
/// Dönem Ortalamaları sayfasının tek cevabı: dönem-dönem ANO/AGNO satırları + toplam
/// (denenmiş) kredi/AKTS özeti. Toplamlar daha önce ayrı bir transkript çağrısından
/// (footer) geliyordu; tek uçta birleştirildi — böylece sayfa ekstra izin/çağrı gerektirmez.
/// </summary>
public class TermGpaHistoryDto
{
    public List<TermGpaHistoryItemDto> Terms { get; set; } = new();

    /// <summary>
    /// Tüm öğrenim boyunca denenmiş toplam ulusal kredi (ders tekrarlarında son alınan ders sayılır).
    /// </summary>
    public int TotalAttemptedCredits { get; set; }

    /// <summary>Tüm öğrenim boyunca denenmiş toplam AKTS (ders tekrarlarında son alınan ders sayılır).</summary>
    public int TotalAttemptedEcts { get; set; }
}
