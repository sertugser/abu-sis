namespace SIS.Domain.Enums;

public enum IneligibilityReason
{
    PrerequisiteNotMet,
    EctsLimitExceeded,
    AlreadyPassed,
    LowCgpaRestriction,
    FinancialBlock,
    StudentNotActive,
    CourseNotInCurriculum,
    ProgramMismatch
}
