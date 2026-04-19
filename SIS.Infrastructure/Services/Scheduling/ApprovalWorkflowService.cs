using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed class ApprovalWorkflowService : IApprovalWorkflowService
{
    private readonly AppDbContext _context;

    public ApprovalWorkflowService(AppDbContext context) => _context = context;

    public async Task<ConstraintExceptionRequest> CreateRequestAsync(ConstraintExceptionRequest request, CancellationToken ct)
    {
        var def = await _context.ConstraintDefinitions.FindAsync(new object[] { request.ConstraintDefinitionId }, ct)
            ?? throw new InvalidOperationException("Constraint tanımı bulunamadı.");

        var steps = GetApprovalChain(def.Category, def.CriticalityLevel);
        int order = 1;
        foreach (var role in steps)
        {
            request.ApprovalSteps.Add(new ApprovalStep
            {
                StepOrder = order++,
                RequiredRole = role,
                Status = ApprovalStepStatus.Pending,
            });
        }

        _context.ConstraintExceptionRequests.Add(request);
        await _context.SaveChangesAsync(ct);
        return request;
    }

    public async Task<(bool Success, string? Error)> ApproveStepAsync(int requestId, string userId, string? comment, CancellationToken ct)
    {
        var request = await _context.ConstraintExceptionRequests
            .Include(r => r.ApprovalSteps.OrderBy(s => s.StepOrder))
            .Include(r => r.ConstraintDefinition)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (request == null) return (false, "Talep bulunamadı.");

        var currentStep = request.ApprovalSteps.FirstOrDefault(s => s.Status == ApprovalStepStatus.Pending);
        if (currentStep == null) return (false, "Onay bekleyen adım yok.");

        currentStep.Status = ApprovalStepStatus.Approved;
        currentStep.DecidedByUserId = userId;
        currentStep.DecidedAt = DateTime.UtcNow;
        currentStep.Comment = comment;

        var nextStep = request.ApprovalSteps.FirstOrDefault(s => s.StepOrder == currentStep.StepOrder + 1);
        if (nextStep == null)
        {
            // All steps completed — create the actual ConstraintException
            request.Status = ExceptionRequestStatus.ApprovedByAdmin;
            var exception = new ConstraintException
            {
                ConstraintDefinitionId = request.ConstraintDefinitionId,
                InstructorId = request.InstructorId,
                StudentId = request.StudentId,
                DepartmentId = request.DepartmentId,
                CourseSectionId = request.CourseSectionId,
                TermId = request.TermId,
                IsExempt = request.IsExempt,
                ParameterOverride = request.ParameterOverride,
                Reason = $"[Onaylı Talep #{request.Id}] {request.Reason}",
                CreatedByUserId = userId,
            };
            _context.ConstraintExceptions.Add(exception);
        }
        else
        {
            request.Status = currentStep.RequiredRole switch
            {
                "DepartmentManager" => ExceptionRequestStatus.ApprovedByManager,
                "Dean" => ExceptionRequestStatus.ApprovedByDean,
                _ => request.Status,
            };
        }

        await _context.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RejectStepAsync(int requestId, string userId, string? comment, CancellationToken ct)
    {
        var request = await _context.ConstraintExceptionRequests
            .Include(r => r.ApprovalSteps.OrderBy(s => s.StepOrder))
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (request == null) return (false, "Talep bulunamadı.");

        var currentStep = request.ApprovalSteps.FirstOrDefault(s => s.Status == ApprovalStepStatus.Pending);
        if (currentStep == null) return (false, "Onay bekleyen adım yok.");

        currentStep.Status = ApprovalStepStatus.Rejected;
        currentStep.DecidedByUserId = userId;
        currentStep.DecidedAt = DateTime.UtcNow;
        currentStep.Comment = comment;

        request.Status = ExceptionRequestStatus.Rejected;

        await _context.SaveChangesAsync(ct);
        return (true, null);
    }

    private static List<string> GetApprovalChain(string category, CriticalityLevel criticality)
    {
        if (category == "Soft")
            return new List<string> { "DepartmentManager" };

        if (criticality == CriticalityLevel.Critical)
            return new List<string> { "DepartmentManager", "Dean", "SystemAdministrator" };

        // Hard but not critical
        return new List<string> { "DepartmentManager", "Dean" };
    }
}
