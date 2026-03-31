using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class Student
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;   // unique system-generated
    // Stored encrypted at persistence layer - never log or expose in API responses
    public string NationalId { get; set; } = string.Empty;
    public StudentStatus Status { get; set; }
    public decimal Cgpa { get; set; }
    public decimal Sgpa { get; set; }
    public int CompletedSemesters { get; set; }                  // total semesters (excluding frozen)
    public int FrozenSemesterCount { get; set; }
    public int WithdrawalCount { get; set; }                     // cumulative withdrawals
    public bool IsScholarshipStudent { get; set; }               // FR-52: skip payment confirmation
    public bool HasFinancialBlock { get; set; }                  // FR-49: debt block
    public int? AdvisorId { get; set; }                          // FK to Instructor
    public Instructor? Advisor { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ICollection<StudentProgram> StudentPrograms { get; set; } = new List<StudentProgram>();
    public ICollection<CourseRegistration> Registrations { get; set; } = new List<CourseRegistration>();
}
