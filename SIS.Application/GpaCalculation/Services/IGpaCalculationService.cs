using SIS.Application.GpaCalculation.DTOs;

namespace SIS.Application.GpaCalculation.Services;

public interface IGpaCalculationService
{
    Task<decimal> CalculateSgpaAsync(int studentId, int termId);
    Task<decimal> CalculateCgpaAsync(int studentId);
    Task<List<TermGpaHistoryItemDto>> GetTermGpaHistoryAsync(int studentId);
    Task<GpaResultDto> RecalculateAndUpdateAsync(int studentId);
}
