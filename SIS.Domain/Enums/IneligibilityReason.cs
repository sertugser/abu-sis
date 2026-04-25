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
    ProgramMismatch,
    // Proliz "Diğer Ön Koşul Tanımları" — kayıt anında uygulanan course-level kurallar
    MinCreditsNotMet,
    MinEctsNotMet,
    HasFailedLowerCourses,
    MustBeTakenStandalone
}
