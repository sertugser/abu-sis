using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

// ApplicationUser lives in SIS.Infrastructure.Persistence (IdentityUser<int> subclass)

namespace SIS.Tests.Application;

public class SectionPickerServiceTests
{
    // ── helpers ─────────────────────────────────────────────────────────

    private static AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    /// <summary>
    /// Simple ISystemConfigProvider fake that always returns the fallback value.
    /// HideQuotaInCatalog defaults to false → QuotaVisible = true.
    /// </summary>
    private sealed class FakeSystemConfigProvider : ISystemConfigProvider
    {
        public Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default) => Task.FromResult(fallback);
        public Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default) => Task.FromResult(fallback);
        public Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default) => Task.FromResult(fallback);
        public Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default) => Task.FromResult(fallback);
        public void InvalidateCache(string key) { }
    }

    /// <summary>Seed an active term with Id=1, then return the term Id.</summary>
    private static AcademicTerm SeedActiveTerm(AppDbContext ctx)
    {
        var term = new AcademicTerm
        {
            Id = 1,
            Name = "Güz 2025-26",
            ShortName = "G25",
            EnglishName = "Fall 2025-26",
            ShortEnglishName = "F25",
            Year = 2025,
            TermType = TermType.Fall,
            TermStart = new DateTime(2025, 9, 1),
            TermEnd = new DateTime(2026, 1, 31),
            IsActivated = true,
            IsCancelled = false,
            CreatedAt = DateTime.UtcNow
        };
        ctx.AcademicTerms.Add(term);
        return term;
    }

    /// <summary>Seed a course with Id=10 belonging to department 1.</summary>
    private static Course SeedCourse(AppDbContext ctx)
    {
        var dept = new Department { Id = 1, FacultyId = 2, Name = "Bilgisayar Mühendisliği" };
        ctx.Departments.Add(dept);

        var course = new Course
        {
            Id = 10,
            Code = "BLM301",
            Name = "Veri Tabanı",
            DepartmentId = 1,
            Ects = 5,
            NationalCredit = 3,
            TheoryHours = 3,
            LabHours = 0,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Courses.Add(course);
        return course;
    }

    /// <summary>Seed FacultyScheduleApproval so gate passes for faculty 2 in term 1.</summary>
    private static void SeedFacultyApproval(AppDbContext ctx, int termId = 1, int facultyId = 2)
    {
        ctx.FacultyScheduleApprovals.Add(new FacultyScheduleApproval { FacultyId = facultyId, TermId = termId });
    }

    // ── Test 1: Basic eligibility rules ──────────────────────────────────

    [Fact]
    public async Task QueryAsync_ReturnsThreeSections_WithCorrectEligibility()
    {
        using var ctx = CreateContext();
        var term = SeedActiveTerm(ctx);
        SeedCourse(ctx);
        SeedFacultyApproval(ctx); // faculty 2, term 1 → gate open for dept-1 courses

        // Instructor for section A
        ctx.Instructors.Add(new Instructor
        {
            Id = 1, UserId = 100, DepartmentId = 1, Title = "Dr.",
            CreatedAt = DateTime.UtcNow
        });

        // ApplicationUser for instructor A
        ctx.Users.Add(new ApplicationUser
        {
            Id = 100, UserName = "drali", Email = "ali@example.com",
            FirstName = "Ali", LastName = "Yılmaz",
            NormalizedUserName = "DRALI", NormalizedEmail = "ALI@EXAMPLE.COM",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        // Section A: Active + faculty approved + not suspended → IsEligible = true
        ctx.CourseSections.Add(new CourseSection
        {
            Id = 1, CourseId = 10, TermId = 1, SectionNumber = 1,
            Status = CourseSectionStatus.Active, IsEnrollmentSuspended = false,
            InstructorId = 1, Capacity = 30, EnrolledCount = 10,
            CreatedAt = DateTime.UtcNow
        });

        // Section B: Draft → yayın öncesi GİZLİ (listede görünmez)
        ctx.CourseSections.Add(new CourseSection
        {
            Id = 2, CourseId = 10, TermId = 1, SectionNumber = 2,
            Status = CourseSectionStatus.Draft, IsEnrollmentSuspended = false,
            InstructorId = null, Capacity = 30, EnrolledCount = 0,
            CreatedAt = DateTime.UtcNow
        });

        // Section C: Active + suspended → gate blocks → IsEligible = false
        ctx.CourseSections.Add(new CourseSection
        {
            Id = 3, CourseId = 10, TermId = 1, SectionNumber = 3,
            Status = CourseSectionStatus.Active, IsEnrollmentSuspended = true,
            InstructorId = null, Capacity = 30, EnrolledCount = 0,
            CreatedAt = DateTime.UtcNow
        });

        await ctx.SaveChangesAsync();

        var gate = new RegistrationGate(ctx);
        var config = new FakeSystemConfigProvider();
        var service = new SectionPickerService(ctx, gate, config);

        var result = await service.QueryAsync(
            studentId: 42,
            new SectionPickerQuery(10, null, null, null, null, 1, 20));

        // Draft (B) yayın öncesi gizlenir → yalnızca A + C listelenir.
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);

        var sectionA = result.Items.Single(i => i.SectionId == 1);
        sectionA.IsEligible.Should().BeTrue();
        sectionA.IneligibleReason.Should().BeNull();
        sectionA.QuotaVisible.Should().BeTrue();

        // Draft şube listede HİÇ görünmemeli (yayın öncesi gizli).
        result.Items.Should().NotContain(i => i.SectionId == 2);

        // Suspended şube görünür ama uygun değil (yayınlanmış, geçici kapalı).
        var sectionC = result.Items.Single(i => i.SectionId == 3);
        sectionC.IsEligible.Should().BeFalse();
        sectionC.IneligibleReason.Should().NotBeNullOrEmpty();
    }

    // ── Test 2: Instructor filter ────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_InstructorFilter_ReturnsSingleSection()
    {
        using var ctx = CreateContext();
        SeedActiveTerm(ctx);
        SeedCourse(ctx);
        SeedFacultyApproval(ctx);

        ctx.Instructors.Add(new Instructor { Id = 1, UserId = 100, DepartmentId = 1, Title = "Prof.", CreatedAt = DateTime.UtcNow });
        ctx.Instructors.Add(new Instructor { Id = 2, UserId = 101, DepartmentId = 1, Title = "Dr.", CreatedAt = DateTime.UtcNow });

        ctx.Users.Add(new ApplicationUser
        {
            Id = 100, UserName = "prof1", Email = "p1@example.com",
            FirstName = "Ayşe", LastName = "Kaya",
            NormalizedUserName = "PROF1", NormalizedEmail = "P1@EXAMPLE.COM",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        ctx.Users.Add(new ApplicationUser
        {
            Id = 101, UserName = "dr2", Email = "p2@example.com",
            FirstName = "Mehmet", LastName = "Demir",
            NormalizedUserName = "DR2", NormalizedEmail = "P2@EXAMPLE.COM",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        // Section A: instructor 1
        ctx.CourseSections.Add(new CourseSection
        {
            Id = 1, CourseId = 10, TermId = 1, SectionNumber = 1,
            Status = CourseSectionStatus.Active, IsEnrollmentSuspended = false,
            InstructorId = 1, Capacity = 30, EnrolledCount = 5,
            CreatedAt = DateTime.UtcNow
        });

        // Section B: instructor 2
        ctx.CourseSections.Add(new CourseSection
        {
            Id = 2, CourseId = 10, TermId = 1, SectionNumber = 2,
            Status = CourseSectionStatus.Active, IsEnrollmentSuspended = false,
            InstructorId = 2, Capacity = 30, EnrolledCount = 5,
            CreatedAt = DateTime.UtcNow
        });

        await ctx.SaveChangesAsync();

        var service = new SectionPickerService(ctx, new RegistrationGate(ctx), new FakeSystemConfigProvider());

        var result = await service.QueryAsync(
            studentId: 42,
            new SectionPickerQuery(10, InstructorId: 1, null, null, null, 1, 20));

        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].SectionId.Should().Be(1);
    }

    // ── Test: Clamp invalid paging inputs ────────────────────────────────

    [Fact]
    public async Task QueryAsync_ClampsInvalidPaging()
    {
        using var ctx = CreateContext();
        SeedActiveTerm(ctx);
        SeedCourse(ctx);
        SeedFacultyApproval(ctx);

        // Seed one section so Items count is verifiable
        ctx.CourseSections.Add(new CourseSection
        {
            Id = 1, CourseId = 10, TermId = 1, SectionNumber = 1,
            Status = CourseSectionStatus.Active, IsEnrollmentSuspended = false,
            InstructorId = null, Capacity = 30, EnrolledCount = 0,
            CreatedAt = DateTime.UtcNow
        });

        await ctx.SaveChangesAsync();

        var service = new SectionPickerService(ctx, new RegistrationGate(ctx), new FakeSystemConfigProvider());

        // Page=0, PageSize=0 — both invalid; service must not throw and must clamp
        var result = await service.QueryAsync(
            studentId: 1,
            new SectionPickerQuery(10, null, null, null, null, Page: 0, PageSize: 0));

        result.Page.Should().BeGreaterThanOrEqualTo(1);
        result.PageSize.Should().BeGreaterThanOrEqualTo(1);
        result.Items.Count.Should().Be(result.TotalCount); // single page covers all items
        result.TotalCount.Should().Be(1);
    }

    // ── Test 3: Pagination ────────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_Pagination_ReturnsCorrectPages()
    {
        using var ctx = CreateContext();
        SeedActiveTerm(ctx);
        SeedCourse(ctx);
        SeedFacultyApproval(ctx);

        // Seed 3 sections without instructors (dept course, faculty approved)
        for (int i = 1; i <= 3; i++)
        {
            ctx.CourseSections.Add(new CourseSection
            {
                Id = i, CourseId = 10, TermId = 1, SectionNumber = i,
                Status = CourseSectionStatus.Active, IsEnrollmentSuspended = false,
                InstructorId = null, Capacity = 30, EnrolledCount = 0,
                CreatedAt = DateTime.UtcNow
            });
        }

        await ctx.SaveChangesAsync();

        var service = new SectionPickerService(ctx, new RegistrationGate(ctx), new FakeSystemConfigProvider());

        // Page 1
        var page1 = await service.QueryAsync(
            studentId: 1,
            new SectionPickerQuery(10, null, null, null, null, Page: 1, PageSize: 2));

        page1.TotalCount.Should().Be(3);
        page1.Items.Should().HaveCount(2);

        // Page 2
        var page2 = await service.QueryAsync(
            studentId: 1,
            new SectionPickerQuery(10, null, null, null, null, Page: 2, PageSize: 2));

        page2.TotalCount.Should().Be(3);
        page2.Items.Should().HaveCount(1);
    }
}
