using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Identity;

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
    public DbSet<TermAcademicCalendar> TermAcademicCalendars => Set<TermAcademicCalendar>();
    public DbSet<OsymRegistrationCalendar> OsymRegistrationCalendars => Set<OsymRegistrationCalendar>();
    public DbSet<ProgramQuota> ProgramQuotas => Set<ProgramQuota>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
public DbSet<GradingCriteria> GradingCriteria => Set<GradingCriteria>();
    public DbSet<GradeComponent> GradeComponents => Set<GradeComponent>();
    public DbSet<StudentGrade> StudentGrades => Set<StudentGrade>();
    public DbSet<CourseSectionApproval> CourseSectionApprovals => Set<CourseSectionApproval>();
    public DbSet<SchedulingAuditLog> SchedulingAuditLogs => Set<SchedulingAuditLog>();
    public DbSet<TermCalendarEvent> TermCalendarEvents => Set<TermCalendarEvent>();
    public DbSet<CalendarTemplate> CalendarTemplates => Set<CalendarTemplate>();
    public DbSet<HolidayDefinition> HolidayDefinitions => Set<HolidayDefinition>();
    public DbSet<CalendarConstraint> CalendarConstraints => Set<CalendarConstraint>();
    public DbSet<GeneratedCalendarDraft> GeneratedCalendarDrafts => Set<GeneratedCalendarDraft>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<SectionSchedule> SectionSchedules => Set<SectionSchedule>();
    public DbSet<ClassroomTermAssignment> ClassroomTermAssignments => Set<ClassroomTermAssignment>();
    public DbSet<CourseAccessPolicy> CourseAccessPolicies => Set<CourseAccessPolicy>();
    public DbSet<RegistrationPlan> RegistrationPlans => Set<RegistrationPlan>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<PromotionRule> PromotionRules => Set<PromotionRule>();
    public DbSet<FacultyScheduleApproval> FacultyScheduleApprovals => Set<FacultyScheduleApproval>();
    public DbSet<ConstraintDefinition> ConstraintDefinitions => Set<ConstraintDefinition>();
    public DbSet<ConstraintException> ConstraintExceptions => Set<ConstraintException>();
    public DbSet<ElectiveGroup> ElectiveGroups => Set<ElectiveGroup>();
    public DbSet<ElectiveGroupCourse> ElectiveGroupCourses => Set<ElectiveGroupCourse>();
    public DbSet<CurriculumElectivePool> CurriculumElectivePools => Set<CurriculumElectivePool>();
    public DbSet<CurriculumTermRule> CurriculumTermRules => Set<CurriculumTermRule>();
    public DbSet<CourseAccessRule> CourseAccessRules => Set<CourseAccessRule>();
    public DbSet<ForecastCalibration> ForecastCalibrations => Set<ForecastCalibration>();
    public DbSet<ScheduleRun> ScheduleRuns => Set<ScheduleRun>();
    public DbSet<EvaluationPeriod> EvaluationPeriods => Set<EvaluationPeriod>();
    public DbSet<EvaluationQuestion> EvaluationQuestions => Set<EvaluationQuestion>();
    public DbSet<EvaluationResponse> EvaluationResponses => Set<EvaluationResponse>();
    public DbSet<EvaluationAnswer> EvaluationAnswers => Set<EvaluationAnswer>();
    public DbSet<InternalAnnouncement> InternalAnnouncements => Set<InternalAnnouncement>();
    public DbSet<ConstraintProfile> ConstraintProfiles => Set<ConstraintProfile>();
    public DbSet<ConstraintProfileItem> ConstraintProfileItems => Set<ConstraintProfileItem>();
    public DbSet<DistributionConstraint> DistributionConstraints => Set<DistributionConstraint>();
    public DbSet<DistributionConstraintCourse> DistributionConstraintCourses => Set<DistributionConstraintCourse>();
    public DbSet<ConstraintExceptionRequest> ConstraintExceptionRequests => Set<ConstraintExceptionRequest>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
    public DbSet<ConstraintViolationSnapshot> ConstraintViolationSnapshots => Set<ConstraintViolationSnapshot>();
    public DbSet<ConstraintViolationSnapshotDetail> ConstraintViolationSnapshotDetails => Set<ConstraintViolationSnapshotDetail>();
    public DbSet<AdvisorCourseConstraint> AdvisorCourseConstraints => Set<AdvisorCourseConstraint>();
    public DbSet<CourseCrossList> CourseCrossLists => Set<CourseCrossList>();

    // ── RBAC (Phase 1) ──────────────────────────────────────────
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<MenuModule> MenuModules => Set<MenuModule>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<MenuItemPermission> MenuItemPermissions => Set<MenuItemPermission>();

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
            e.HasIndex(p => p.ExternalSystemCode)
                .IsUnique()
                .HasFilter("\"ExternalSystemCode\" IS NOT NULL");
            e.Property(p => p.ExternalSystemCode).HasMaxLength(30);

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

        // ── CourseSection.CrossListGroupId — index for group lookup ──
        builder.Entity<CourseSection>(e =>
        {
            e.HasIndex(cs => cs.CrossListGroupId);
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

            e.HasOne(cc => cc.ElectiveGroup)
             .WithMany()
             .HasForeignKey(cc => cc.ElectiveGroupId)
             .OnDelete(DeleteBehavior.SetNull);
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

        // ── ElectiveGroup ────────────────────────────────────────
        builder.Entity<ElectiveGroup>(e =>
        {
            e.HasIndex(eg => eg.Code).IsUnique();

            e.HasOne(eg => eg.Faculty)
             .WithMany()
             .HasForeignKey(eg => eg.FacultyId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── ElectiveGroupCourse (composite PK) ──────────────────
        builder.Entity<ElectiveGroupCourse>(e =>
        {
            e.HasKey(egc => new { egc.ElectiveGroupId, egc.CourseId });

            e.HasOne(egc => egc.ElectiveGroup)
             .WithMany(eg => eg.Courses)
             .HasForeignKey(egc => egc.ElectiveGroupId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(egc => egc.Course)
             .WithMany()
             .HasForeignKey(egc => egc.CourseId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── CurriculumElectivePool ──────────────────────────────
        builder.Entity<CurriculumElectivePool>(e =>
        {
            e.HasIndex(cep => new { cep.CurriculumId, cep.ElectiveGroupId }).IsUnique();

            e.HasOne(cep => cep.Curriculum)
             .WithMany(c => c.ElectivePools)
             .HasForeignKey(cep => cep.CurriculumId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(cep => cep.ElectiveGroup)
             .WithMany(eg => eg.CurriculumPools)
             .HasForeignKey(cep => cep.ElectiveGroupId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── CurriculumTermRule (Müfredat Dönem Kuralları) ───────
        builder.Entity<CurriculumTermRule>(e =>
        {
            // Aynı (Curriculum, Sınıf, Dönem, Kural Tipi) kombinasyonu benzersizdir.
            e.HasIndex(r => new { r.CurriculumId, r.ClassYear, r.TermHalf, r.RuleType }).IsUnique();

            e.Property(r => r.Value).HasColumnType("numeric(10,2)");

            e.HasOne(r => r.Curriculum)
             .WithMany(c => c.TermRules)
             .HasForeignKey(r => r.CurriculumId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── CourseAccessRule ────────────────────────────────────
        builder.Entity<CourseAccessRule>(e =>
        {
            e.HasIndex(car => car.CourseId);

            e.Property(car => car.MinCgpa).HasPrecision(4, 2);

            e.HasOne(car => car.Course)
             .WithMany()
             .HasForeignKey(car => car.CourseId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(car => car.AllowedFaculty)
             .WithMany()
             .HasForeignKey(car => car.AllowedFacultyId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(car => car.AllowedDepartment)
             .WithMany()
             .HasForeignKey(car => car.AllowedDepartmentId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(car => car.AllowedProgram)
             .WithMany()
             .HasForeignKey(car => car.AllowedProgramId)
             .OnDelete(DeleteBehavior.SetNull);
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
            e.HasIndex(cs => new { cs.CourseId, cs.TermId, cs.SectionNumber }).IsUnique();
            e.HasIndex(cs => new { cs.CourseId, cs.TermId });

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

            e.Property(cs => cs.RowVersion)
             .IsRowVersion();
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

        // ── CourseSectionApproval ──────────────────────────────
        builder.Entity<CourseSectionApproval>(e =>
        {
            e.HasIndex(a => new { a.CourseId, a.DepartmentId, a.TermId }).IsUnique();

            e.HasOne(a => a.Course)
             .WithMany()
             .HasForeignKey(a => a.CourseId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Department)
             .WithMany()
             .HasForeignKey(a => a.DepartmentId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Term)
             .WithMany()
             .HasForeignKey(a => a.TermId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── SchedulingAuditLog ──────────────────────────────────
        builder.Entity<SchedulingAuditLog>(entity =>
        {
            entity.HasOne(e => e.Section)
                .WithMany()
                .HasForeignKey(e => e.SectionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.SectionId, e.CreatedAt });
        });

        // ── AcademicTerm ───────────────────────────────────────
        builder.Entity<AcademicTerm>(e =>
        {
            e.HasIndex(t => new { t.Year, t.TermType }).IsUnique();
        });

        // ── TermAcademicCalendar (1-1 AcademicTerm) ────────────
        builder.Entity<TermAcademicCalendar>(e =>
        {
            e.HasOne(c => c.Term)
             .WithOne(t => t.AcademicCalendar)
             .HasForeignKey<TermAcademicCalendar>(c => c.TermId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(c => c.TermId).IsUnique();
        });

        // ── TermCalendarEvent ──────────────────────────────────
        builder.Entity<TermCalendarEvent>(e =>
        {
            e.HasOne(ev => ev.Term)
             .WithMany(t => t.CalendarEvents)
             .HasForeignKey(ev => ev.TermId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(ev => new { ev.TermId, ev.StartDate });
            e.HasIndex(ev => new { ev.IsGenerated, ev.GeneratedDraftId });
        });

        // ── Academic Calendar Generator ────────────────────────
        builder.Entity<CalendarTemplate>(e =>
        {
            e.Property(t => t.Name).HasMaxLength(160);
            e.Property(t => t.ChainCode).HasMaxLength(40);
            e.Property(t => t.ItemsJson).HasColumnType("jsonb");
            e.HasIndex(t => t.ChainCode).IsUnique();
        });

        builder.Entity<HolidayDefinition>(e =>
        {
            e.Property(h => h.Name).HasMaxLength(160);
            e.Property(h => h.EnglishName).HasMaxLength(160);
            e.Property(h => h.Source).HasMaxLength(80);
            e.Property(h => h.DurationDays).HasPrecision(4, 1);
            e.Property(h => h.OverridesJson).HasColumnType("jsonb");
            e.HasIndex(h => h.Name).IsUnique();
        });

        builder.Entity<CalendarConstraint>(e =>
        {
            e.Property(c => c.AcademicYear).HasMaxLength(20);
            e.Property(c => c.TargetChainCode).HasMaxLength(40);
            e.Property(c => c.ParameterJson).HasColumnType("jsonb");
            e.HasIndex(c => new { c.AcademicYear, c.ConstraintType });
        });

        builder.Entity<GeneratedCalendarDraft>(e =>
        {
            e.Property(d => d.AcademicYear).HasMaxLength(20);
            e.Property(d => d.EventsJson).HasColumnType("jsonb");
            e.Property(d => d.HolidaysJson).HasColumnType("jsonb");
            e.Property(d => d.WarningsJson).HasColumnType("jsonb");
            e.Property(d => d.PreviousSnapshotJson).HasColumnType("jsonb");
            e.HasIndex(d => new { d.AcademicYear, d.Status });
            e.HasIndex(d => d.ExpiresAt);
        });

        // ── TimeSlot ─────────────────────────────────────────────
        builder.Entity<TimeSlot>(e =>
        {
            e.HasIndex(ts => new { ts.TermId, ts.DayOfWeek, ts.StartTime }).IsUnique();

            e.HasOne(ts => ts.Term)
             .WithMany()
             .HasForeignKey(ts => ts.TermId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── SectionSchedule ──────────────────────────────────────
        builder.Entity<SectionSchedule>(e =>
        {
            // Lab split desteği: aynı section + aynı slot, FARKLI derslik kombinasyonuna izin ver.
            // Kapasite yetmeyen lab oturumları 2+ oda'ya bölünebilir (ör. BM101 lab L139+L141 aynı anda).
            // ClassroomId'nin index'e dahil edilmesi (section, slot, classroom) üçlüsünü unique yapar.
            e.HasIndex(ss => new { ss.CourseSectionId, ss.TimeSlotId, ss.ClassroomId }).IsUnique();

            e.HasOne(ss => ss.CourseSection)
             .WithMany(cs => cs.SectionSchedules)
             .HasForeignKey(ss => ss.CourseSectionId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(ss => ss.TimeSlot)
             .WithMany()
             .HasForeignKey(ss => ss.TimeSlotId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ss => ss.Classroom)
             .WithMany()
             .HasForeignKey(ss => ss.ClassroomId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ClassroomTermAssignment ──────────────────────────────
        builder.Entity<ClassroomTermAssignment>(e =>
        {
            e.HasIndex(cta => new { cta.TermId, cta.ClassroomId }).IsUnique();

            e.HasOne(cta => cta.Term)
             .WithMany()
             .HasForeignKey(cta => cta.TermId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cta => cta.Classroom)
             .WithMany()
             .HasForeignKey(cta => cta.ClassroomId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cta => cta.Faculty)
             .WithMany()
             .HasForeignKey(cta => cta.FacultyId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── CourseAccessPolicy ───────────────────────────────────
        builder.Entity<CourseAccessPolicy>(e =>
        {
            e.HasOne(cap => cap.Course)
             .WithMany()
             .HasForeignKey(cap => cap.CourseId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(cap => cap.Term)
             .WithMany()
             .HasForeignKey(cap => cap.TermId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cap => cap.Faculty)
             .WithMany()
             .HasForeignKey(cap => cap.FacultyId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cap => cap.Department)
             .WithMany()
             .HasForeignKey(cap => cap.DepartmentId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── RegistrationPlan ─────────────────────────────────────
        builder.Entity<RegistrationPlan>(e =>
        {
            e.HasIndex(rp => new { rp.StudentId, rp.TermId }).IsUnique();

            e.HasOne(rp => rp.Student)
             .WithMany()
             .HasForeignKey(rp => rp.StudentId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(rp => rp.Term)
             .WithMany()
             .HasForeignKey(rp => rp.TermId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── AdvisorCourseConstraint ─────────────────────────────
        builder.Entity<AdvisorCourseConstraint>(e =>
        {
            e.HasIndex(ac => new { ac.StudentId, ac.CourseId, ac.TermId }).IsUnique();
            e.HasIndex(ac => new { ac.StudentId, ac.TermId, ac.IsActive });

            e.Property(ac => ac.Note).HasMaxLength(500);

            e.HasOne(ac => ac.Student)
             .WithMany()
             .HasForeignKey(ac => ac.StudentId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ac => ac.Course)
             .WithMany()
             .HasForeignKey(ac => ac.CourseId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ac => ac.Term)
             .WithMany()
             .HasForeignKey(ac => ac.TermId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ac => ac.Advisor)
             .WithMany()
             .HasForeignKey(ac => ac.AdvisorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── CourseRegistration → RegistrationPlan FK ─────────────
        builder.Entity<CourseRegistration>(e2 =>
        {
            e2.HasOne(cr => cr.RegistrationPlan)
              .WithMany(rp => rp.Registrations)
              .HasForeignKey(cr => cr.RegistrationPlanId)
              .OnDelete(DeleteBehavior.SetNull);

            // Havuz snapshot FK (null = zorunlu veya tekil seçmeli)
            e2.HasOne(cr => cr.AppliedElectivePool)
              .WithMany()
              .HasForeignKey(cr => cr.AppliedElectivePoolId)
              .OnDelete(DeleteBehavior.SetNull);
        });

        // ── CourseCrossList (M:N Course ↔ Course) ───────────────
        builder.Entity<CourseCrossList>(e =>
        {
            e.HasKey(x => new { x.CourseId, x.LinkedCourseId });
            e.HasIndex(x => x.LinkedCourseId);

            e.HasOne(x => x.Course)
             .WithMany()
             .HasForeignKey(x => x.CourseId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.LinkedCourse)
             .WithMany()
             .HasForeignKey(x => x.LinkedCourseId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── AuditLog ─────────────────────────────────────────────
        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(al => al.Timestamp);
            e.HasIndex(al => new { al.EntityType, al.EntityId });
            e.HasIndex(al => al.UserId);
        });

        // ── SystemConfig ─────────────────────────────────────────
        builder.Entity<SystemConfig>(e =>
        {
            e.HasIndex(sc => sc.Key).IsUnique();
            e.Property(sc => sc.RowVersion)
             .IsRowVersion();
        });

        // ── UserPreference ───────────────────────────────────────
        builder.Entity<UserPreference>(e =>
        {
            e.HasIndex(up => up.UserId).IsUnique();
            e.Property(up => up.FavoritesJson).HasColumnType("jsonb");

            e.HasOne<ApplicationUser>()
             .WithOne()
             .HasForeignKey<UserPreference>(up => up.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── PromotionRule ────────────────────────────────────────
        builder.Entity<PromotionRule>(e =>
        {
            // γ hibrit unique: aynı program×müfredat×fromYear×toYear kombinasyonu tek olabilir.
            // CurriculumId null olabilir → program-bazlı default; set ise müfredat-spesifik override.
            e.HasIndex(pr => new { pr.ProgramId, pr.CurriculumId, pr.FromYear, pr.ToYear }).IsUnique();
            e.Property(pr => pr.MinCgpa).HasPrecision(4, 2);

            e.HasOne(pr => pr.Program)
             .WithMany()
             .HasForeignKey(pr => pr.ProgramId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(pr => pr.Curriculum)
             .WithMany()
             .HasForeignKey(pr => pr.CurriculumId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Classroom ────────────────────────────────────────────
        builder.Entity<Classroom>(e =>
        {
            e.HasIndex(c => c.Name).IsUnique();
        });

        // ── FacultyScheduleApproval ──────────────────────────────
        builder.Entity<FacultyScheduleApproval>(e =>
        {
            e.HasIndex(fsa => new { fsa.FacultyId, fsa.TermId }).IsUnique();

            e.HasOne(fsa => fsa.Faculty)
             .WithMany()
             .HasForeignKey(fsa => fsa.FacultyId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(fsa => fsa.Term)
             .WithMany()
             .HasForeignKey(fsa => fsa.TermId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ConstraintDefinition ────────────────────────────────
        builder.Entity<ConstraintDefinition>(e =>
        {
            e.HasIndex(cd => cd.Code).IsUnique();
            e.Property(cd => cd.Code).HasMaxLength(10);
            e.Property(cd => cd.Name).HasMaxLength(100);
            e.Property(cd => cd.Category).HasMaxLength(10);
            e.Property(cd => cd.Scope).HasMaxLength(20);
        });

        // ── ConstraintException ─────────────────────────────────
        builder.Entity<ConstraintException>(e =>
        {
            e.HasIndex(ce => new { ce.ConstraintDefinitionId, ce.InstructorId, ce.DepartmentId, ce.CourseSectionId, ce.TermId });
            e.HasOne(ce => ce.ConstraintDefinition).WithMany(d => d.Exceptions).HasForeignKey(ce => ce.ConstraintDefinitionId);
            e.HasOne(ce => ce.Instructor).WithMany().HasForeignKey(ce => ce.InstructorId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(ce => ce.Student).WithMany().HasForeignKey(ce => ce.StudentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(ce => ce.Department).WithMany().HasForeignKey(ce => ce.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(ce => ce.CourseSection).WithMany().HasForeignKey(ce => ce.CourseSectionId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(ce => ce.Term).WithMany().HasForeignKey(ce => ce.TermId).OnDelete(DeleteBehavior.SetNull);
        });

        // ── ConstraintProfile ────────────────────────────────────
        builder.Entity<ConstraintProfileItem>(e =>
        {
            e.HasIndex(i => new { i.ProfileId, i.ConstraintDefinitionId }).IsUnique();

            e.HasOne(i => i.Profile)
             .WithMany(p => p.Items)
             .HasForeignKey(i => i.ProfileId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(i => i.ConstraintDefinition)
             .WithMany()
             .HasForeignKey(i => i.ConstraintDefinitionId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── DistributionConstraint ──────────────────────────────
        builder.Entity<DistributionConstraint>(e =>
        {
            e.HasIndex(d => d.TermId);
            e.HasOne(d => d.Term).WithMany().HasForeignKey(d => d.TermId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Department).WithMany().HasForeignKey(d => d.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<DistributionConstraintCourse>(e =>
        {
            e.HasIndex(c => new { c.DistributionConstraintId, c.CourseId, c.CourseSectionId });
            e.HasOne(c => c.DistributionConstraint).WithMany(d => d.Courses).HasForeignKey(c => c.DistributionConstraintId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Course).WithMany().HasForeignKey(c => c.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.CourseSection).WithMany().HasForeignKey(c => c.CourseSectionId).OnDelete(DeleteBehavior.SetNull);
        });

        // ── ConstraintExceptionRequest ──────────────────────────
        builder.Entity<ConstraintExceptionRequest>(e =>
        {
            e.HasIndex(r => r.Status);
            e.HasOne(r => r.ConstraintDefinition).WithMany().HasForeignKey(r => r.ConstraintDefinitionId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── ApprovalStep ────────────────────────────────────────
        builder.Entity<ApprovalStep>(e =>
        {
            e.HasIndex(s => new { s.RequestId, s.StepOrder });
            e.HasOne(s => s.Request).WithMany(r => r.ApprovalSteps).HasForeignKey(s => s.RequestId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── ConstraintViolationSnapshot ────────────────────────────
        builder.Entity<ConstraintViolationSnapshot>(e =>
        {
            e.HasIndex(s => new { s.TermId, s.SnapshotAt });
            e.HasOne(s => s.Term).WithMany().HasForeignKey(s => s.TermId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ConstraintViolationSnapshotDetail>(e =>
        {
            e.HasIndex(d => d.SnapshotId);
            e.HasOne(d => d.Snapshot).WithMany(s => s.Details).HasForeignKey(d => d.SnapshotId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Performance Indexes ──────────────────────────────────
        builder.Entity<CourseRegistration>(e3 =>
        {
            e3.HasIndex(cr => cr.StudentId);
            e3.HasIndex(cr => cr.CourseSectionId);
        });

        builder.Entity<Student>(e2 =>
        {
            e2.HasIndex(s => s.AdvisorId);
        });

        builder.Entity<GradeComponent>(e2 =>
        {
            e2.HasIndex(gc => new { gc.CourseId, gc.AcademicTermId, gc.Name }).IsUnique();
        });

        builder.Entity<Curriculum>(e2 =>
        {
            e2.HasIndex(c => new { c.ProgramId, c.Version }).IsUnique();
        });

        // ── ForecastCalibration ──────────────────────────────────
        builder.Entity<ForecastCalibration>(entity =>
        {
            entity.HasOne(fc => fc.Course).WithMany().HasForeignKey(fc => fc.CourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(fc => fc.Term).WithMany().HasForeignKey(fc => fc.TermId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(fc => fc.Department).WithMany().HasForeignKey(fc => fc.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(fc => new { fc.CourseId, fc.TermId }).IsUnique();
        });

        // ── ScheduleRun ──────────────────────────────────────────
        builder.Entity<ScheduleRun>(entity =>
        {
            entity.HasOne(sr => sr.Faculty).WithMany().HasForeignKey(sr => sr.FacultyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(sr => sr.Term).WithMany().HasForeignKey(sr => sr.TermId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(sr => new { sr.FacultyId, sr.TermId, sr.CreatedAt });
        });

        // ── EvaluationPeriod ─────────────────────────────────────
        builder.Entity<EvaluationPeriod>(e =>
        {
            e.HasIndex(ep => ep.TermId).IsUnique();

            e.HasOne(ep => ep.Term)
             .WithMany()
             .HasForeignKey(ep => ep.TermId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── EvaluationQuestion ───────────────────────────────────
        builder.Entity<EvaluationQuestion>(e =>
        {
            e.Property(q => q.Text).HasMaxLength(500);
            e.HasIndex(q => new { q.Category, q.SortOrder });
        });

        // ── EvaluationResponse ───────────────────────────────────
        builder.Entity<EvaluationResponse>(e =>
        {
            // One evaluation per (period, section, student)
            e.HasIndex(r => new { r.EvaluationPeriodId, r.CourseSectionId, r.StudentId }).IsUnique();
            e.HasIndex(r => r.CourseSectionId);

            e.HasOne(r => r.EvaluationPeriod)
             .WithMany()
             .HasForeignKey(r => r.EvaluationPeriodId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.CourseSection)
             .WithMany()
             .HasForeignKey(r => r.CourseSectionId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.Student)
             .WithMany()
             .HasForeignKey(r => r.StudentId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── EvaluationAnswer ─────────────────────────────────────
        builder.Entity<EvaluationAnswer>(e =>
        {
            e.Property(a => a.TextValue).HasMaxLength(2000);

            e.HasOne(a => a.Response)
             .WithMany(r => r.Answers)
             .HasForeignKey(a => a.ResponseId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.Question)
             .WithMany()
             .HasForeignKey(a => a.QuestionId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── InternalAnnouncement ─────────────────────────────────
        builder.Entity<InternalAnnouncement>(e =>
        {
            e.Property(a => a.Title).HasMaxLength(200);
            e.Property(a => a.Content).HasMaxLength(5000);
            e.Property(a => a.CreatedBy).HasMaxLength(100);
            e.Property(a => a.CreatedByName).HasMaxLength(200);

            e.HasIndex(a => new { a.IsActive, a.StartsAt, a.ExpiresAt });
            e.HasIndex(a => a.TargetAudience);

            e.HasOne(a => a.TargetDepartment)
             .WithMany()
             .HasForeignKey(a => a.TargetDepartmentId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── OsymRegistrationCalendar ───────────────────────────
        builder.Entity<OsymRegistrationCalendar>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => new { x.Year, x.Type });
        });

        // ── ProgramQuota ────────────────────────────────────────
        builder.Entity<ProgramQuota>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasOne(q => q.Program)
             .WithMany()
             .HasForeignKey(q => q.ProgramId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(q => new { q.ProgramId, q.Year, q.Type }).IsUnique();
        });

        // ── RBAC: Permission ────────────────────────────────────
        builder.Entity<Permission>(e =>
        {
            e.Property(p => p.Key).HasMaxLength(150).IsRequired();
            e.Property(p => p.Description).HasMaxLength(500);
            e.HasIndex(p => p.Key).IsUnique();
            e.HasIndex(p => p.Category);
        });

        // ── RBAC: RolePermission (composite PK) ─────────────────
        builder.Entity<RolePermission>(e =>
        {
            e.HasKey(rp => new { rp.RoleId, rp.PermissionId });
            e.HasOne<IdentityRole<int>>()
             .WithMany()
             .HasForeignKey(rp => rp.RoleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(rp => rp.Permission)
             .WithMany(p => p.RolePermissions)
             .HasForeignKey(rp => rp.PermissionId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(rp => rp.PermissionId);
        });

        // ── RBAC: MenuModule (recursive tree) ───────────────────
        builder.Entity<MenuModule>(e =>
        {
            e.Property(m => m.Slug).HasMaxLength(100).IsRequired();
            e.Property(m => m.Label).HasMaxLength(100).IsRequired();
            e.Property(m => m.Icon).HasMaxLength(50);
            e.Property(m => m.AllowedRolesCsv).HasMaxLength(200);
            e.HasOne(m => m.Parent)
             .WithMany(m => m.Children)
             .HasForeignKey(m => m.ParentId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => m.Slug).IsUnique();
            e.HasIndex(m => m.ParentId);
            e.HasIndex(m => new { m.ParentId, m.SortOrder });
        });

        // ── RBAC: MenuItem ──────────────────────────────────────
        // Route UNIQUE kaldırıldı — aynı route farklı modüllerde olabilir (örn. "Duyurular"
        // hem "Genel İşlemler" hem "Duyuru Yönetimi" altında). Composite (ModuleId, Route)
        // UNIQUE: aynı modülde aynı route iki kez yasak.
        builder.Entity<MenuItem>(e =>
        {
            e.Property(mi => mi.Label).HasMaxLength(150).IsRequired();
            e.Property(mi => mi.Route).HasMaxLength(200).IsRequired();
            e.Property(mi => mi.Slug).HasMaxLength(100).IsRequired();
            e.HasOne(mi => mi.Module)
             .WithMany(m => m.Items)
             .HasForeignKey(mi => mi.ModuleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(mi => mi.Route);
            e.HasIndex(mi => new { mi.ModuleId, mi.Route }).IsUnique();
            e.HasIndex(mi => new { mi.ModuleId, mi.SortOrder });
        });

        // ── RBAC: MenuItemPermission (composite PK) ─────────────
        builder.Entity<MenuItemPermission>(e =>
        {
            e.HasKey(mip => new { mip.MenuItemId, mip.PermissionId });
            e.HasOne(mip => mip.MenuItem)
             .WithMany(mi => mi.MenuItemPermissions)
             .HasForeignKey(mip => mip.MenuItemId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(mip => mip.Permission)
             .WithMany(p => p.MenuItemPermissions)
             .HasForeignKey(mip => mip.PermissionId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
