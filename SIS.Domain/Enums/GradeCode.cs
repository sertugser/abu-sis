namespace SIS.Domain.Enums;

public enum GradeCode
{
    AA, BA, BB, CB, CC, DC, DD, FF,
    W,   // Withdrawal
    I,   // Incomplete
    NI,  // Non-Included (excluded from GPA, non-repeatable)
    NA,  // Non-Attendance (treated as FF/0.00 in GPA)
    S,   // Satisfactory (non-GPA course passed)
    U    // Unsatisfactory (non-GPA course failed)
}
