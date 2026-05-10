namespace SIS.Domain.Enums;

public enum AnnouncementAudience
{
    // ── System-wide (SysAdmin / AdministrativeStaff) ──────────────────────────
    All         = 0,
    Students    = 1,
    Instructors = 2,
    Management  = 4,   // DeptManager + Dean + AdministrativeStaff

    // ── Department-scoped (DepartmentManager → kendi bölümü) ─────────────────
    Department            = 3,   // bölümdeki herkes
    DepartmentStudents    = 5,   // yalnızca bölüm öğrencileri
    DepartmentInstructors = 6,   // yalnızca bölüm öğretim elemanları

    // ── Faculty-scoped (Dean → kendi fakültesi) ───────────────────────────────
    Faculty            = 7,    // fakültedeki herkes
    FacultyStudents    = 8,    // yalnızca fakülte öğrencileri
    FacultyInstructors = 9,    // yalnızca fakülte öğretim elemanları
    FacultyManagement  = 10,   // yalnızca fakülte bölüm başkanları

    // ── Course-section-scoped (Instructor → verdiği dersin öğrencileri) ───────
    CourseSection = 11,

    // ── Advisor-scoped (AcademicAdvisor → danışmanı olduğu öğrenciler) ────────
    MyAdvisees = 20,

    // ── Flat role-based targets (yeni sistem — belirli bir role gönderim) ─────
    AcademicAdvisors        = 12,   // hedef: tüm AcademicAdvisor kullanıcıları
    DepartmentManagers      = 13,   // hedef: tüm DepartmentManager kullanıcıları
    Deans                   = 14,   // hedef: tüm Dean kullanıcıları
    AdministrativeStaffRole = 15,   // hedef: tüm AdministrativeStaff kullanıcıları

    // ── Rol kapsamı "Hepsi" değerleri (gönderenin izin verilen tüm kitleleri) ─
    AllForSysAdmin  = 16,   // SysAdmin → AdminStaff + Dean + DeptMgr + Advisor + Instructor + Student
    AllForAdmin     = 17,   // AdministrativeStaff → Dean + DeptMgr + Advisor + Instructor + Student
    AllForDean      = 18,   // Dean → DeptMgr + Advisor + Instructor + Student
    AllForDeptMgr   = 19,   // DepartmentManager → Advisor + Instructor + Student
}
