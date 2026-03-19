namespace SIS.Domain.Enums;

public enum RegistrationStatus
{
    Draft,                      // plan item, soft hold
    PendingAdvisorApproval,
    Active,
    Dropped,
    Withdrawn,
    Completed                   // grade entered, course finished
}
