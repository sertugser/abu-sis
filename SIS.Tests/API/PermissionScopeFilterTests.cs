using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Identity.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.API;

public class PermissionScopeFilterTests
{
    private sealed class StubScope : ICurrentUserScope
    {
        public bool IsActive { get; init; }
        public int? FacultyId { get; init; }
        public Task<bool> IsActiveAsync() => Task.FromResult(IsActive);
        public Task<int?> GetFacultyIdAsync() => Task.FromResult(FacultyId);
    }

    private static AppDbContext CreateContext(ICurrentUserScope scope) =>
        new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"Filter-{Guid.NewGuid()}")
                .Options,
            scope);

    [Fact]
    public async Task Faculty_Filter_ReturnsOnlyScopedFaculty_WhenActive()
    {
        await using var db = CreateContext(new StubScope { IsActive = true, FacultyId = 1 });
        db.Faculties.AddRange(
            new Faculty { Id = 1, Name = "Engineering", Code = "ENG" },
            new Faculty { Id = 2, Name = "Medicine", Code = "MED" });
        await db.SaveChangesAsync();

        var result = await db.Faculties.ToListAsync();

        result.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public async Task Department_Filter_ReturnsOnlyScopedFacultyDepartments()
    {
        await using var db = CreateContext(new StubScope { IsActive = true, FacultyId = 1 });
        db.Faculties.AddRange(
            new Faculty { Id = 1, Name = "Engineering", Code = "ENG" },
            new Faculty { Id = 2, Name = "Medicine", Code = "MED" });
        db.Departments.AddRange(
            new Department { Id = 10, Name = "Computer Engineering", Code = "CENG", FacultyId = 1 },
            new Department { Id = 11, Name = "Cardiology", Code = "CARD", FacultyId = 2 });
        await db.SaveChangesAsync();

        var result = await db.Departments.ToListAsync();

        result.Should().ContainSingle().Which.Id.Should().Be(10);
    }

    [Fact]
    public async Task AcademicProgram_Filter_CascadesThroughDepartment()
    {
        await using var db = CreateContext(new StubScope { IsActive = true, FacultyId = 1 });
        db.Faculties.AddRange(
            new Faculty { Id = 1, Name = "Engineering", Code = "ENG" },
            new Faculty { Id = 2, Name = "Medicine", Code = "MED" });
        db.Departments.AddRange(
            new Department { Id = 10, Name = "Computer Engineering", Code = "CENG", FacultyId = 1 },
            new Department { Id = 11, Name = "Cardiology", Code = "CARD", FacultyId = 2 });
        db.AcademicPrograms.AddRange(
            new AcademicProgram
            {
                Id = 100,
                Name = "Computer Engineering",
                Code = "CENG-BSC",
                DepartmentId = 10,
                DegreeLevel = DegreeLevel.Bachelor,
                EducationType = EducationType.Semester,
                MaxStudyDurationSemesters = 14,
                TotalRequiredEcts = 240,
            },
            new AcademicProgram
            {
                Id = 101,
                Name = "Medicine",
                Code = "MED-INT",
                DepartmentId = 11,
                DegreeLevel = DegreeLevel.Bachelor,
                EducationType = EducationType.Semester,
                MaxStudyDurationSemesters = 14,
                TotalRequiredEcts = 360,
            });
        await db.SaveChangesAsync();

        var result = await db.AcademicPrograms.ToListAsync();

        result.Should().ContainSingle().Which.Id.Should().Be(100);
    }

    [Fact]
    public async Task Course_Filter_CascadesThroughDepartment()
    {
        await using var db = CreateContext(new StubScope { IsActive = true, FacultyId = 1 });
        db.Faculties.AddRange(
            new Faculty { Id = 1, Name = "Engineering", Code = "ENG" },
            new Faculty { Id = 2, Name = "Medicine", Code = "MED" });
        db.Departments.AddRange(
            new Department { Id = 10, Name = "Computer Engineering", Code = "CENG", FacultyId = 1 },
            new Department { Id = 11, Name = "Cardiology", Code = "CARD", FacultyId = 2 });
        db.Courses.AddRange(
            new Course { Id = 1000, Code = "CENG101", Name = "Intro to Programming", DepartmentId = 10, Ects = 5, TheoryHours = 3, LabHours = 2 },
            new Course { Id = 1001, Code = "CARD101", Name = "Cardiology Intro", DepartmentId = 11, Ects = 5, TheoryHours = 3, LabHours = 2 });
        await db.SaveChangesAsync();

        var result = await db.Courses.ToListAsync();

        result.Should().ContainSingle().Which.Id.Should().Be(1000);
    }

    [Fact]
    public async Task Student_Filter_CascadesThroughStudentPrograms()
    {
        await using var db = CreateContext(new StubScope { IsActive = true, FacultyId = 1 });
        db.Faculties.AddRange(
            new Faculty { Id = 1, Name = "Engineering", Code = "ENG" },
            new Faculty { Id = 2, Name = "Medicine", Code = "MED" });
        db.Departments.AddRange(
            new Department { Id = 10, Name = "Computer Engineering", Code = "CENG", FacultyId = 1 },
            new Department { Id = 11, Name = "Cardiology", Code = "CARD", FacultyId = 2 });
        db.AcademicPrograms.AddRange(
            new AcademicProgram
            {
                Id = 100,
                Name = "Computer Engineering",
                Code = "CENG-BSC",
                DepartmentId = 10,
                DegreeLevel = DegreeLevel.Bachelor,
                EducationType = EducationType.Semester,
                MaxStudyDurationSemesters = 14,
                TotalRequiredEcts = 240,
            },
            new AcademicProgram
            {
                Id = 101,
                Name = "Medicine",
                Code = "MED-INT",
                DepartmentId = 11,
                DegreeLevel = DegreeLevel.Bachelor,
                EducationType = EducationType.Semester,
                MaxStudyDurationSemesters = 14,
                TotalRequiredEcts = 360,
            });
        db.Curricula.AddRange(
            new Curriculum { Id = 500, ProgramId = 100, Version = "2024", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Curriculum { Id = 501, ProgramId = 101, Version = "2024", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Students.AddRange(
            new Student { Id = 1, UserId = 1001, StudentNumber = "2201001", NationalId = "11111111111", Status = StudentStatus.Active },
            new Student { Id = 2, UserId = 1002, StudentNumber = "2201002", NationalId = "22222222222", Status = StudentStatus.Active });
        db.StudentPrograms.AddRange(
            new StudentProgram
            {
                StudentId = 1,
                ProgramId = 100,
                CurriculumId = 500,
                Type = StudentProgramType.Main,
                EnrollmentDate = DateTime.UtcNow,
                IsActive = true,
            },
            new StudentProgram
            {
                StudentId = 2,
                ProgramId = 101,
                CurriculumId = 501,
                Type = StudentProgramType.Main,
                EnrollmentDate = DateTime.UtcNow,
                IsActive = true,
            });
        await db.SaveChangesAsync();

        var result = await db.Students.ToListAsync();

        result.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public async Task IgnoreQueryFilters_BypassesScope()
    {
        await using var db = CreateContext(new StubScope { IsActive = true, FacultyId = 1 });
        db.Faculties.AddRange(
            new Faculty { Id = 1, Name = "Engineering", Code = "ENG" },
            new Faculty { Id = 2, Name = "Medicine", Code = "MED" });
        await db.SaveChangesAsync();

        var result = await db.Faculties.IgnoreQueryFilters().ToListAsync();

        result.Should().HaveCount(2);
    }
}
