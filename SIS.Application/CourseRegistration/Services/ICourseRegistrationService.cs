using SIS.Application.CourseRegistration.DTOs;

namespace SIS.Application.CourseRegistration.Services;

public interface ICourseRegistrationService
{
    Task<RegistrationResponseDto> AddToPlanAsync(int studentId, int courseSectionId);
    Task<RegistrationResponseDto> SubmitRegistrationAsync(int studentId, RegistrationSubmitDto dto);
    Task<List<StudentRegistrationDto>> GetStudentRegistrationsAsync(int studentId);
    Task<List<CourseHistoryTermDto>> GetCourseHistoryAsync(int studentId);
    Task<RegistrationResponseDto> DropCourseAsync(int studentId, int registrationId);
    Task<RegistrationResponseDto> WithdrawCourseAsync(int studentId, int registrationId);

    // Advisor approval methods
    Task<List<PendingApprovalDto>> GetPendingApprovalsAsync(int advisorInstructorId);
    Task<ApprovalResponseDto> ApproveStudentRegistrationsAsync(int advisorInstructorId, ApprovalActionDto dto);
    Task<ApprovalResponseDto> RejectStudentRegistrationsAsync(int advisorInstructorId, ApprovalActionDto dto);
}
