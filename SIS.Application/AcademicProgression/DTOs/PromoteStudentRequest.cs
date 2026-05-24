namespace SIS.Application.AcademicProgression.DTOs;

/// <summary>
/// Sınıf atlatma isteği. TargetLevel null → kurallara göre terfi,
/// dolu → manuel override (kurallar atlanır).
/// </summary>
public record PromoteStudentRequest(int? TargetLevel);
