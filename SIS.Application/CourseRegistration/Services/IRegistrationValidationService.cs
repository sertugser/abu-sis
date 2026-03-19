using SIS.Application.CourseRegistration.DTOs;

namespace SIS.Application.CourseRegistration.Services;

public interface IRegistrationValidationService
{
    Task<ValidationResultDto> ValidateRegistrationAsync(int studentId, List<int> courseSectionIds);
}
