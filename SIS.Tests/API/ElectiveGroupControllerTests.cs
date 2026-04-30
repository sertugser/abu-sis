using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Tests.TestHelpers;

namespace SIS.Tests.API;

public class ElectiveGroupControllerTests : IDisposable
{
    private readonly AppDbContext _context;

    private const int FacultyId = 1;
    private const int DepartmentId = 1;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;

    public ElectiveGroupControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        SeedBaseData();
    }

    private void SeedBaseData()
    {
        _context.Faculties.Add(new Faculty { Id = FacultyId, Name = "Engineering", Code = "ENG" });
        _context.Departments.Add(new Department { Id = DepartmentId, Name = "Computer Eng", Code = "CE", FacultyId = FacultyId });
        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId, Name = "Computer Engineering", Code = "CE-BSC",
            DepartmentId = DepartmentId, DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester, MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240
        });
        _context.Curricula.Add(new Curriculum
        {
            Id = CurriculumId, ProgramId = ProgramId, Version = "2024", IsActive = true, CreatedAt = DateTime.UtcNow
        });
        _context.Courses.AddRange(
            new Course { Id = 1, Code = "TE101", Name = "Elective 1", Ects = 3 },
            new Course { Id = 2, Code = "TE102", Name = "Elective 2", Ects = 3 },
            new Course { Id = 3, Code = "OC101", Name = "Univ Elective 1", Ects = 3 }
        );
        _context.SaveChanges();
        _context.SeedElectiveSubtypeDefaults();
    }

    [Fact]
    public async Task ElectiveGroup_Create_And_AddCourses_Works()
    {
        var group = new ElectiveGroup
        {
            Name = "Teknik Seçmeli", Code = "TE-ENG",
            ElectiveSubtypeId = TestSeedExtensions.TechnicalElectiveId,
            FacultyId = FacultyId, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        _context.ElectiveGroups.Add(group);
        await _context.SaveChangesAsync();

        group.Id.Should().BeGreaterThan(0);

        _context.ElectiveGroupCourses.Add(new ElectiveGroupCourse { ElectiveGroupId = group.Id, CourseId = 1, AddedAt = DateTime.UtcNow });
        _context.ElectiveGroupCourses.Add(new ElectiveGroupCourse { ElectiveGroupId = group.Id, CourseId = 2, AddedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var courses = await _context.ElectiveGroupCourses
            .Where(egc => egc.ElectiveGroupId == group.Id)
            .ToListAsync();
        courses.Should().HaveCount(2);
    }

    [Fact]
    public async Task CurriculumElectivePool_UniqueConstraint_Works()
    {
        var group = new ElectiveGroup
        {
            Name = "OC Pool", Code = "OC-UNI",
            ElectiveSubtypeId = TestSeedExtensions.UniversityElectiveId,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        _context.ElectiveGroups.Add(group);
        await _context.SaveChangesAsync();

        var pool = new CurriculumElectivePool
        {
            CurriculumId = CurriculumId, ElectiveGroupId = group.Id,
            SuggestedSemesterStart = 1, SuggestedSemesterEnd = 8,
            RequiredCourseCount = 4, RequiredEcts = 12,
            CreatedAt = DateTime.UtcNow
        };
        _context.CurriculumElectivePools.Add(pool);
        await _context.SaveChangesAsync();

        pool.Id.Should().BeGreaterThan(0);

        var loadedPool = await _context.CurriculumElectivePools
            .Include(p => p.ElectiveGroup)
            .FirstAsync(p => p.Id == pool.Id);
        loadedPool.ElectiveGroup.Name.Should().Be("OC Pool");
        loadedPool.RequiredCourseCount.Should().Be(4);
    }

    [Fact]
    public async Task CourseAccessRule_Create_Works()
    {
        var rule = new CourseAccessRule
        {
            CourseId = 1,
            AllowedFacultyId = FacultyId,
            MinClassYear = 3,
            MinCgpa = 2.5m,
            RequiresAdvisorApproval = true,
            IsOpenToOwnDepartment = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.CourseAccessRules.Add(rule);
        await _context.SaveChangesAsync();

        rule.Id.Should().BeGreaterThan(0);

        var loaded = await _context.CourseAccessRules.FindAsync(rule.Id);
        loaded!.MinCgpa.Should().Be(2.5m);
        loaded.RequiresAdvisorApproval.Should().BeTrue();
        loaded.IsOpenToOwnDepartment.Should().BeFalse();
    }

    public void Dispose() => _context.Dispose();
}
