using SIS.Application.StudentCard.DTOs;

namespace SIS.Application.StudentCard.Services;

/// <summary>
/// Transkript Senaryosu (what-if) hesabının TEK kaynağı. Öğrencinin ekranda simüle ettiği
/// ders listesini alır, harf→puan / geçti / GPA'ya girer kurallarını GradePointMap + GpaMath
/// üzerinden uygular ve dönem/genel ortalamaları döner. Frontend bu hesabı tekrar yapmaz.
/// </summary>
public interface ITranscriptScenarioService
{
    ScenarioReportDto Calculate(TranscriptScenarioRequestDto request);
}
