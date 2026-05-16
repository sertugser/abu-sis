using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class AdvisorInfoServiceTests
{
    // ── Factory helpers ───────────────────────────────────────────────────

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdvisorInfoTest-{Guid.NewGuid()}")
            .Options);

    private static AdvisorInfoService CreateService(AppDbContext ctx) => new(ctx);

    private static (ApplicationUser User, Student Student, ApplicationUser AdvisorUser, Instructor Advisor, AcademicProgram Program, Curriculum Curriculum)
        SeedFullGraph(AppDbContext ctx, bool withAdvisor = true)
    {
        var faculty = new Faculty { Name = "Mühendislik ve Mimarlık Fakültesi", Code = "MMF" };
        ctx.Faculties.Add(faculty);
        ctx.SaveChanges();

        var dept = new Department
        {
            Name = "Bilgisayar Mühendisliği (İngilizce)",
            Code = "CENG",
            FacultyId = faculty.Id
        };
        ctx.Departments.Add(dept);
        ctx.SaveChanges();

        var program = new AcademicProgram
        {
            Name = "Bilgisayar Mühendisliği (İngilizce)",
            Code = "CENG-EN",
            DepartmentId = dept.Id
        };
        ctx.AcademicPrograms.Add(program);
        ctx.SaveChanges();

        var curriculum = new Curriculum
        {
            ProgramId = program.Id,
            Version = "2024",
            IsActive = true,
            Status = CurriculumStatus.Published
        };
        ctx.Curricula.Add(curriculum);
        ctx.SaveChanges();

        var studentUser = new ApplicationUser
        {
            Id = 100,
            UserName = "student@uni.tr",
            Email = "student@uni.tr",
            FirstName = "Ali",
            LastName = "Veli"
        };
        ctx.Users.Add(studentUser);

        Instructor? advisor = null;
        ApplicationUser? advisorUser = null;
        if (withAdvisor)
        {
            advisorUser = new ApplicationUser
            {
                Id = 200,
                UserName = "advisor@uni.tr",
                Email = "phd.mesut@gmail.com",
                FirstName = "Mesut",
                LastName = "ÜNLÜ",
                PhoneNumber = "0312 000 00 00"
            };
            ctx.Users.Add(advisorUser);
            ctx.SaveChanges();

            advisor = new Instructor
            {
                UserId = advisorUser.Id,
                DepartmentId = dept.Id,
                Title = "Dr.Öğretim Üyesi",
                MaxWeeklyHours = 40,
                CreatedAt = DateTime.UtcNow
            };
            ctx.Instructors.Add(advisor);
            ctx.SaveChanges();
        }

        var student = new Student
        {
            UserId = studentUser.Id,
            StudentNumber = "20240001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            AdvisorId = advisor?.Id,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Students.Add(student);
        ctx.SaveChanges();

        ctx.StudentPrograms.Add(new StudentProgram
        {
            StudentId = student.Id,
            ProgramId = program.Id,
            CurriculumId = curriculum.Id,
            Type = StudentProgramType.Main,
            IsActive = true,
            EnrollmentDate = DateTime.UtcNow
        });
        ctx.SaveChanges();

        return (studentUser, student, advisorUser!, advisor!, program, curriculum);
    }

    private static AcademicTerm SeedActiveTerm(AppDbContext ctx, DateTime? now = null)
    {
        var n = now ?? DateTime.UtcNow;
        var term = new AcademicTerm
        {
            Name = "2025-2026 Güz",
            ShortName = "2025G",
            EnglishName = "2025-2026 Fall",
            ShortEnglishName = "25F",
            Year = 2025,
            TermType = TermType.Fall,
            TermStart = n.AddDays(-30),
            TermEnd = n.AddDays(60),
            IsCancelled = false,
            CreatedAt = DateTime.UtcNow
        };
        ctx.AcademicTerms.Add(term);
        ctx.SaveChanges();
        return term;
    }

    // ── GetMyAdvisorAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetMyAdvisorAsync_ReturnsHasAdvisorFalse_WhenStudentRecordMissing()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);

        var result = await svc.GetMyAdvisorAsync(userId: 999);

        result.HasAdvisor.Should().BeFalse();
        result.FullName.Should().BeNull();
    }

    [Fact]
    public async Task GetMyAdvisorAsync_ReturnsHasAdvisorFalse_WhenAdvisorNotAssigned()
    {
        using var ctx = CreateContext();
        var (user, _, _, _, _, _) = SeedFullGraph(ctx, withAdvisor: false);
        var svc = CreateService(ctx);

        var result = await svc.GetMyAdvisorAsync(user.Id);

        result.HasAdvisor.Should().BeFalse();
        result.FullName.Should().BeNull();
    }

    [Fact]
    public async Task GetMyAdvisorAsync_ReturnsFullDto_WhenAdvisorAssigned()
    {
        using var ctx = CreateContext();
        var (user, _, _, _, _, _) = SeedFullGraph(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetMyAdvisorAsync(user.Id);

        result.HasAdvisor.Should().BeTrue();
        result.Title.Should().Be("Dr.Öğretim Üyesi");
        result.FirstName.Should().Be("Mesut");
        result.LastName.Should().Be("ÜNLÜ");
        result.FullName.Should().Be("Dr.Öğretim Üyesi Mesut ÜNLÜ");
        result.FacultyName.Should().Be("Mühendislik ve Mimarlık Fakültesi");
        result.DepartmentName.Should().Be("Bilgisayar Mühendisliği (İngilizce)");
        result.ProgramName.Should().Be("Bilgisayar Mühendisliği (İngilizce)");
        result.Phone.Should().Be("0312 000 00 00");
        result.Email.Should().Be("phd.mesut@gmail.com");
    }

    [Fact]
    public async Task GetMyAdvisorAsync_FullName_OmitsTitle_WhenTitleEmpty()
    {
        using var ctx = CreateContext();
        var (user, _, _, advisor, _, _) = SeedFullGraph(ctx);
        advisor.Title = string.Empty;
        ctx.SaveChanges();
        var svc = CreateService(ctx);

        var result = await svc.GetMyAdvisorAsync(user.Id);

        result.FullName.Should().Be("Mesut ÜNLÜ");
    }

    // ── GetAdvisorScheduleAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetScheduleAsync_Throws_WhenStudentMissing()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);

        Func<Task> act = () => svc.GetAdvisorScheduleAsync(userId: 999);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetScheduleAsync_Throws_WhenAdvisorNotAssigned()
    {
        using var ctx = CreateContext();
        var (user, _, _, _, _, _) = SeedFullGraph(ctx, withAdvisor: false);
        var svc = CreateService(ctx);

        Func<Task> act = () => svc.GetAdvisorScheduleAsync(user.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetScheduleAsync_ReturnsEmpty_WhenNoActiveTerm()
    {
        using var ctx = CreateContext();
        var (user, _, _, _, _, _) = SeedFullGraph(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetAdvisorScheduleAsync(user.Id);

        result.TermLabel.Should().BeNull();
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetScheduleAsync_ReturnsEmpty_WhenAdvisorHasNoActiveSections()
    {
        using var ctx = CreateContext();
        var (user, _, _, _, _, _) = SeedFullGraph(ctx);
        SeedActiveTerm(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetAdvisorScheduleAsync(user.Id);

        result.TermLabel.Should().Be("2025-2026 Güz");
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetScheduleAsync_ReturnsScheduleItems_WhenAdvisorTeaches()
    {
        using var ctx = CreateContext();
        var (user, _, _, advisor, _, _) = SeedFullGraph(ctx);
        var term = SeedActiveTerm(ctx);

        var course = new Course
        {
            Code = "CENG201",
            Name = "Data Structures",
            DepartmentId = advisor.DepartmentId,
            NationalCredit = 4,
            Ects = 6,
            TheoryHours = 3,
            LabHours = 0,
            Level = CourseLevel.Undergraduate,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Courses.Add(course);
        ctx.SaveChanges();

        var section = new CourseSection
        {
            CourseId = course.Id,
            TermId = term.Id,
            SectionNumber = 1,
            InstructorId = advisor.Id,
            Status = CourseSectionStatus.Active,
            Capacity = 40,
            CreatedAt = DateTime.UtcNow
        };
        ctx.CourseSections.Add(section);
        ctx.SaveChanges();

        var classroom = new Classroom
        {
            Name = "A101",
            BuildingName = "A Blok",
            Floor = 1,
            Capacity = 50,
            Type = ClassroomType.Lecture,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Classrooms.Add(classroom);
        ctx.SaveChanges();

        var slot = new TimeSlot
        {
            TermId = term.Id,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 50),
            SlotOrder = 1,
            DisplayName = "Pazartesi 1. Ders (09:00-09:50)"
        };
        ctx.TimeSlots.Add(slot);
        ctx.SaveChanges();

        ctx.SectionSchedules.Add(new SectionSchedule
        {
            CourseSectionId = section.Id,
            TimeSlotId = slot.Id,
            ClassroomId = classroom.Id,
            BlockType = ScheduleBlockType.Theory
        });
        ctx.SaveChanges();

        var svc = CreateService(ctx);

        var result = await svc.GetAdvisorScheduleAsync(user.Id);

        result.TermLabel.Should().Be("2025-2026 Güz");
        result.Items.Should().HaveCount(1);
        result.Items[0].CourseCode.Should().Be("CENG201");
        result.Items[0].CourseName.Should().Be("Data Structures");
        result.Items[0].SectionNumber.Should().Be(1);
        result.Items[0].DayOfWeek.Should().Be(1); // Monday → 1
        result.Items[0].StartTime.Should().Be(new TimeOnly(9, 0));
        result.Items[0].EndTime.Should().Be(new TimeOnly(9, 50));
        result.Items[0].ClassroomCode.Should().Be("A101");  // Projected from Classroom.Name
        result.Items[0].BlockType.Should().Be("Theory");
    }

    [Fact]
    public async Task GetScheduleAsync_ExcludesCancelledSections()
    {
        using var ctx = CreateContext();
        var (user, _, _, advisor, _, _) = SeedFullGraph(ctx);
        var term = SeedActiveTerm(ctx);

        var course = new Course
        {
            Code = "CENG202",
            Name = "Algorithms",
            DepartmentId = advisor.DepartmentId,
            NationalCredit = 4,
            Ects = 6,
            TheoryHours = 3,
            LabHours = 0,
            Level = CourseLevel.Undergraduate,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Courses.Add(course);
        ctx.SaveChanges();

        ctx.CourseSections.Add(new CourseSection
        {
            CourseId = course.Id,
            TermId = term.Id,
            SectionNumber = 1,
            InstructorId = advisor.Id,
            Status = CourseSectionStatus.Cancelled,
            Capacity = 40,
            CreatedAt = DateTime.UtcNow
        });
        ctx.SaveChanges();

        var svc = CreateService(ctx);

        var result = await svc.GetAdvisorScheduleAsync(user.Id);

        result.Items.Should().BeEmpty();
    }
}
