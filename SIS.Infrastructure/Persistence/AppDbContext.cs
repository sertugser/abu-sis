using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // DbSets
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Instructor> Instructors => Set<Instructor>();
    public DbSet<Faculty> Faculties => Set<Faculty>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<AcademicProgram> AcademicPrograms => Set<AcademicProgram>();
    public DbSet<Curriculum> Curricula => Set<Curriculum>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CurriculumCourse> CurriculumCourses => Set<CurriculumCourse>();
    public DbSet<CoursePrerequisite> CoursePrerequisites => Set<CoursePrerequisite>();
    public DbSet<CourseEquivalency> CourseEquivalencies => Set<CourseEquivalency>();
    public DbSet<CourseSection> CourseSections => Set<CourseSection>();
    public DbSet<CourseRegistration> CourseRegistrations => Set<CourseRegistration>();
    public DbSet<StudentProgram> StudentPrograms => Set<StudentProgram>();
    public DbSet<AcademicTerm> AcademicTerms => Set<AcademicTerm>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<InstructorAvailability> InstructorAvailabilities => Set<InstructorAvailability>();
    public DbSet<GradingCriteria> GradingCriteria => Set<GradingCriteria>();
    public DbSet<GradeComponent> GradeComponents => Set<GradeComponent>();
    public DbSet<StudentGrade> StudentGrades => Set<StudentGrade>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Identity tables

        // ── Student ──────────────────────────────────────────────
        builder.Entity<Student>(e =>
        {
            e.HasIndex(s => s.StudentNumber).IsUnique();
            e.HasIndex(s => s.NationalId).IsUnique();
            e.HasIndex(s => s.UserId).IsUnique();
            e.Property(s => s.Cgpa).HasPrecision(4, 2);
            e.Property(s => s.Sgpa).HasPrecision(4, 2);

            e.HasOne<ApplicationUser>()
             .WithOne()
             .HasForeignKey<Student>(s => s.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.Advisor)
             .WithMany()
             .HasForeignKey(s => s.AdvisorId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Instructor ───────────────────────────────────────────
        builder.Entity<Instructor>(e =>
        {
            e.HasIndex(i => i.UserId).IsUnique();

            e.HasOne<ApplicationUser>()
             .WithOne()
             .HasForeignKey<Instructor>(i => i.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(i => i.Department)
             .WithMany()
             .HasForeignKey(i => i.DepartmentId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── InstructorAvailability ───────────────────────────────
        builder.Entity<InstructorAvailability>(e =>
        {
            e.HasIndex(ia => new { ia.InstructorId, ia.DayOfWeek, ia.StartTime, ia.EndTime }).IsUnique();

            e.HasOne(ia => ia.Instructor)
             .WithMany(i => i.Availabilities)
             .HasForeignKey(ia => ia.InstructorId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Faculty ─────────────────────────────────────────────
        builder.Entity<Faculty>(e =>
        {
            e.HasIndex(f => f.Code).IsUnique();
        });

        // ── Department ───────────────────────────────────────────
        builder.Entity<Department>(e =>
        {
            e.HasIndex(d => d.Code).IsUnique();

            e.HasOne(d => d.Faculty)
             .WithMany(f => f.Departments)
             .HasForeignKey(d => d.FacultyId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── AcademicProgram ──────────────────────────────────────
        builder.Entity<AcademicProgram>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();

            e.HasOne(p => p.Department)
             .WithMany(d => d.Programs)
             .HasForeignKey(p => p.DepartmentId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Curriculum ───────────────────────────────────────────
        builder.Entity<Curriculum>(e =>
        {
            e.HasOne(c => c.Program)
             .WithMany(p => p.Curricula)
             .HasForeignKey(c => c.ProgramId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Course ───────────────────────────────────────────────
        builder.Entity<Course>(e =>
        {
            e.HasIndex(c => c.Code).IsUnique();

            e.HasOne(c => c.Department)
             .WithMany()
             .HasForeignKey(c => c.DepartmentId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── CurriculumCourse (composite PK) ─────────────────────
        builder.Entity<CurriculumCourse>(e =>
        {
            e.HasKey(cc => new { cc.CurriculumId, cc.CourseId });

            e.HasOne(cc => cc.Curriculum)
             .WithMany(c => c.CurriculumCourses)
             .HasForeignKey(cc => cc.CurriculumId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cc => cc.Course)
             .WithMany()
             .HasForeignKey(cc => cc.CourseId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── CoursePrerequisite (composite PK) ────────────────────
        builder.Entity<CoursePrerequisite>(e =>
        {
            e.HasKey(cp => new { cp.CourseId, cp.RequiredCourseId });

            e.HasOne(cp => cp.Course)
             .WithMany(c => c.Prerequisites)
             .HasForeignKey(cp => cp.CourseId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cp => cp.RequiredCourse)
             .WithMany()
             .HasForeignKey(cp => cp.RequiredCourseId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── CourseEquivalency (composite PK) ─────────────────────
        builder.Entity<CourseEquivalency>(e =>
        {
            e.HasKey(ce => new { ce.CourseId, ce.EquivalentCourseId });

            e.HasOne(ce => ce.Course)
             .WithMany(c => c.Equivalencies)
             .HasForeignKey(ce => ce.CourseId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ce => ce.EquivalentCourse)
             .WithMany()
             .HasForeignKey(ce => ce.EquivalentCourseId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── StudentProgram (composite PK) ────────────────────────
        builder.Entity<StudentProgram>(e =>
        {
            e.HasKey(sp => new { sp.StudentId, sp.ProgramId });

            e.HasOne(sp => sp.Student)
             .WithMany(s => s.StudentPrograms)
             .HasForeignKey(sp => sp.StudentId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(sp => sp.Program)
             .WithMany()
             .HasForeignKey(sp => sp.ProgramId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(sp => sp.Curriculum)
             .WithMany()
             .HasForeignKey(sp => sp.CurriculumId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── CourseSection ────────────────────────────────────────
        builder.Entity<CourseSection>(e =>
        {
            e.HasOne(cs => cs.Course)
             .WithMany()
             .HasForeignKey(cs => cs.CourseId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cs => cs.Term)
             .WithMany()
             .HasForeignKey(cs => cs.TermId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cs => cs.Instructor)
             .WithMany(i => i.Sections)
             .HasForeignKey(cs => cs.InstructorId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(cs => cs.Classroom)
             .WithMany()
             .HasForeignKey(cs => cs.ClassroomId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── CourseRegistration ───────────────────────────────────
        builder.Entity<CourseRegistration>(e =>
        {
            e.HasIndex(cr => new { cr.StudentId, cr.CourseSectionId }).IsUnique();

            e.HasOne(cr => cr.Student)
             .WithMany(s => s.Registrations)
             .HasForeignKey(cr => cr.StudentId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cr => cr.CourseSection)
             .WithMany(cs => cs.Registrations)
             .HasForeignKey(cr => cr.CourseSectionId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── GradeComponent ─────────────────────────────────────
        builder.Entity<GradeComponent>(e =>
        {
            e.Property(gc => gc.Weight).HasPrecision(5, 2);

            e.HasOne(gc => gc.Course)
             .WithMany()
             .HasForeignKey(gc => gc.CourseId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(gc => gc.AcademicTerm)
             .WithMany()
             .HasForeignKey(gc => gc.AcademicTermId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── StudentGrade ──────────────────────────────────────
        builder.Entity<StudentGrade>(e =>
        {
            // Bir öğrenci bir bileşenden tek not alır
            e.HasIndex(sg => new { sg.CourseRegistrationId, sg.GradeComponentId }).IsUnique();

            e.Property(sg => sg.Score).HasPrecision(5, 2);

            e.HasOne(sg => sg.CourseRegistration)
             .WithMany()
             .HasForeignKey(sg => sg.CourseRegistrationId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(sg => sg.GradeComponent)
             .WithMany(gc => gc.StudentGrades)
             .HasForeignKey(sg => sg.GradeComponentId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── GradingCriteria ─────────────────────────────────────
        builder.Entity<GradingCriteria>(e =>
        {
            // Bir ders bir dönemde tek aralık tanımına sahip olur
            e.HasIndex(gc => new { gc.CourseId, gc.AcademicTermId }).IsUnique();

            e.HasOne(gc => gc.Course)
             .WithMany()
             .HasForeignKey(gc => gc.CourseId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(gc => gc.AcademicTerm)
             .WithMany()
             .HasForeignKey(gc => gc.AcademicTermId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── AcademicTerm ───────────────────────────────────────
        builder.Entity<AcademicTerm>(e =>
        {
            e.HasIndex(t => new { t.Year, t.Semester }).IsUnique();
        });
    }
}
