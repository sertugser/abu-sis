using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.Common.Services;
using SIS.Application.CourseOpening.DTOs;
using SIS.Application.EnrollmentForecast.DTOs;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.CourseOpening;

namespace SIS.Tests.Application;

public class CourseOpeningServiceTests
{
    private const int FacultyId = 1;
    private const int OtherFacultyId = 2;
    private const int DepartmentId = 1;
    private const int OtherDepartmentId = 2;
    private const int OtherFacultyDepartmentId = 3;
    private const int TermId = 1;

    [Fact]
    public async Task UpdateSectionAsync_WhenInstructorBelongsToDifferentFaculty_Throws()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var course = AddCourse(context, 1, "CENG101");
        var section = AddSection(context, 1, course.Id);
        // Instructor belongs to a department in a DIFFERENT faculty — cross-faculty assignment is rejected
        var foreignInstructor = AddInstructor(context, 2, 1002, OtherFacultyDepartmentId);
        var service = CreateService(context);

        var act = () => service.UpdateSectionAsync(new UpdateOpenedSectionRequest
        {
            SectionId = section.Id,
            InstructorId = foreignInstructor.Id
        }, CancellationToken.None);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*bu fakülteye ait değil*");
    }

    [Fact]
    public async Task UpdateSectionAsync_WhenCapacityFallsBelowRegisteredCount_Throws()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var course = AddCourse(context, 1, "CENG201");
        var section = AddSection(context, 1, course.Id, capacity: 40, enrolledCount: 1);
        var student1 = AddStudent(context, 1);
        var student2 = AddStudent(context, 2);

        AddRegistration(context, 1, student1.Id, section.Id, RegistrationStatus.Active);
        AddRegistration(context, 2, student2.Id, section.Id, RegistrationStatus.PendingAdvisorApproval);

        var service = CreateService(context);

        var act = () => service.UpdateSectionAsync(new UpdateOpenedSectionRequest
        {
            SectionId = section.Id,
            Capacity = 1
        }, CancellationToken.None);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*kayıtlı öğrenci sayısından (2) az olamaz*");
    }

    [Fact]
    public async Task CloseSectionAsync_WhenClosingLastActiveSection_RemovesAccessPolicies()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var course = AddCourse(context, 1, "CENG301");
        var section = AddSection(context, 1, course.Id);
        AddAccessPolicy(context, course.Id, DepartmentId);
        AddAccessPolicy(context, course.Id, OtherDepartmentId);

        var service = CreateService(context);

        await service.CloseSectionAsync(section.Id, CancellationToken.None);

        context.CourseAccessPolicies.Should().BeEmpty();
        context.CourseSections.Single(s => s.Id == section.Id).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task CloseSectionAsync_WhenOtherActiveSectionsExist_KeepsAccessPolicies()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var course = AddCourse(context, 1, "CENG302");
        var section1 = AddSection(context, 1, course.Id, sectionNumber: 1);
        var section2 = AddSection(context, 2, course.Id, sectionNumber: 2);
        AddAccessPolicy(context, course.Id, DepartmentId);
        AddAccessPolicy(context, course.Id, OtherDepartmentId);

        var service = CreateService(context);

        await service.CloseSectionAsync(section1.Id, CancellationToken.None);

        context.CourseAccessPolicies.Should().HaveCount(2);
        context.CourseSections.Single(s => s.Id == section1.Id).IsActive.Should().BeFalse();
        context.CourseSections.Single(s => s.Id == section2.Id).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task OpenCourseAsync_WhenNoApproval_CreatesSection()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var course = AddCourse(context, 10, "CENG401");
        var service = CreateService(context);

        var result = await service.OpenCourseAsync(DepartmentId, TermId, new OpenCourseRequest
        {
            CourseId = course.Id,
            SectionCount = 1,
            CapacityPerSection = 30
        }, ct: CancellationToken.None);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task OpenCourseAsync_WhenApprovedQuotaRemains_AppendsNewSectionsWithoutReplacingPolicies()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var course = AddCourse(context, 11, "CENG402");
        AddApproval(context, course.Id, approvedSectionCount: 3, status: SectionApprovalStatus.Approved);
        AddSection(context, 11, course.Id, sectionNumber: 1);
        AddAccessPolicy(context, course.Id, DepartmentId);

        var service = CreateService(context);

        var result = await service.OpenCourseAsync(DepartmentId, TermId, new OpenCourseRequest
        {
            CourseId = course.Id,
            SectionCount = 2,
            CapacityPerSection = 35
        }, ct: CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(section => section.SectionNumber).Should().Equal(2, 3);
        context.CourseAccessPolicies.Should().ContainSingle(policy => policy.CourseId == course.Id && policy.DepartmentId == DepartmentId);
        context.CourseSections.Count(section => section.CourseId == course.Id && section.Status == CourseSectionStatus.Active).Should().Be(3);
    }

    [Fact]
    public async Task GetCurriculumCoursesForOpeningAsync_UsesForecastSummaryAndApprovalWorkflow()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var course = AddCourse(context, 12, "CENG403");
        AddCurriculumCourse(context, course.Id, suggestedSemester: 3);
        AddApproval(context, course.Id, approvedSectionCount: 3, status: SectionApprovalStatus.Approved);
        AddSection(context, 12, course.Id, sectionNumber: 1);

        var forecastServiceMock = new Mock<IEnrollmentForecastService>();
        forecastServiceMock
            .Setup(service => service.GetDepartmentSummaryAsync(DepartmentId))
            .ReturnsAsync(new DepartmentForecastSummaryDto
            {
                DepartmentId = DepartmentId,
                DepartmentName = "Computer Engineering",
                CourseSummaries =
                {
                    new CourseDemandForecastDto
                    {
                        CourseId = course.Id,
                        CourseCode = course.Code,
                        CourseName = course.Name,
                        TotalPredictedDemand = 24,
                        RecommendedSectionCount = 3,
                        ComparableTermEnrollment = 18,
                        DemandBreakdown = new DemandBreakdownDto
                        {
                            RetakeFF = 2,
                            RetakeNA = 1
                        },
                        SectionDistribution = new SectionDistributionDto
                        {
                            EffectiveCapacityPerSection = 35
                        },
                        CrossDepartmentDemand =
                        {
                            new SIS.Application.EnrollmentForecast.DTOs.CrossDepartmentDemandDto
                            {
                                DepartmentId = OtherDepartmentId,
                                DepartmentName = "Electrical Engineering",
                                ProgramName = "EEE",
                                EstimatedStudentCount = 4
                            }
                        }
                    }
                }
            });

        var service = CreateQueryService(context, forecastServiceMock.Object);

        var result = await service.GetCurriculumCoursesForOpeningAsync(DepartmentId, TermId, false, CancellationToken.None);

        var dto = result.Should().ContainSingle().Subject;
        dto.PredictedDemand.Should().Be(24);
        dto.RetakeCount.Should().Be(3);
        dto.RecommendedSectionCount.Should().Be(3);
        dto.RecommendedCapacity.Should().Be(35);
        dto.PreviousTermEnrollment.Should().Be(18);
        dto.CrossDepartmentDemand.Should().ContainSingle(item => item.DepartmentId == OtherDepartmentId && item.EstimatedStudentCount == 4);
        dto.CanOpen.Should().BeTrue();
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static void SeedBaseData(AppDbContext context)
    {
        context.Faculties.AddRange(
            new Faculty
            {
                Id = FacultyId,
                Name = "Engineering",
                Code = "ENG"
            },
            new Faculty
            {
                Id = OtherFacultyId,
                Name = "Science",
                Code = "SCI"
            });

        context.Departments.AddRange(
            new Department
            {
                Id = DepartmentId,
                FacultyId = FacultyId,
                Name = "Computer Engineering",
                Code = "CENG"
            },
            new Department
            {
                Id = OtherDepartmentId,
                FacultyId = FacultyId,
                Name = "Electrical Engineering",
                Code = "EEE"
            },
            new Department
            {
                Id = OtherFacultyDepartmentId,
                FacultyId = OtherFacultyId,
                Name = "Mathematics",
                Code = "MATH"
            });

        var termEnd = DateTime.UtcNow.AddDays(90);
        context.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId,
            Name = "2025-2026 Güz",
            ShortName = "25-26 Güz",
            EnglishName = "Fall 2025-2026",
            Year = 2025,
            TermType = TermType.Fall,
            TermStart = DateTime.UtcNow.AddDays(-14),
            TermEnd = termEnd,
            AcademicCalendar = new SIS.Domain.Entities.TermAcademicCalendar
            {
                RegistrationStart = DateTime.UtcNow.AddDays(-7),
                RegistrationEnd = DateTime.UtcNow.AddDays(7),
                AddDropStart = DateTime.UtcNow.AddDays(8),
                AddDropEnd = DateTime.UtcNow.AddDays(14),
                WithdrawalDeadline = DateTime.UtcNow.AddDays(30),
                GradeEntryStart = termEnd.AddDays(-14),
                GradeEntryEnd = termEnd.AddDays(14),
                CreatedAt = DateTime.UtcNow,
            }
        });

        context.SaveChanges();
    }

    private static Course AddCourse(AppDbContext context, int id, string code)
    {
        var course = new Course
        {
            Id = id,
            Code = code,
            Name = code,
            DepartmentId = DepartmentId,
            Ects = 6,
            TheoryHours = 3,
            LabHours = 0,
            IsNonGpa = false
        };

        context.Courses.Add(course);
        context.SaveChanges();
        return course;
    }

    private static void AddCurriculumCourse(AppDbContext context, int courseId, int suggestedSemester)
    {
        var programId = 500 + courseId;
        var curriculumId = 700 + courseId;

        context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = programId,
            Name = "Computer Engineering",
            Code = $"CENG-{courseId}",
            DepartmentId = DepartmentId,
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240
        });

        context.Curricula.Add(new Curriculum
        {
            Id = curriculumId,
            ProgramId = programId,
            Version = "2025",
            IsActive = true
        });

        context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = curriculumId,
            CourseId = courseId,
            SuggestedSemester = suggestedSemester,
            IsCompulsory = true
        });

        context.SaveChanges();
    }

    private static void AddApproval(AppDbContext context, int courseId, int approvedSectionCount, SectionApprovalStatus status)
    {
        context.CourseSectionApprovals.Add(new CourseSectionApproval
        {
            CourseId = courseId,
            DepartmentId = DepartmentId,
            TermId = TermId,
            RecommendedSectionCount = 2,
            ApprovedSectionCount = approvedSectionCount,
            PredictedDemand = 20,
            Status = status,
            CreatedAt = DateTime.UtcNow
        });

        context.SaveChanges();
    }

    private static Instructor AddInstructor(AppDbContext context, int id, int userId, int departmentId)
    {
        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"instructor{userId}",
            NormalizedUserName = $"INSTRUCTOR{userId}",
            Email = $"instructor{userId}@example.com",
            NormalizedEmail = $"INSTRUCTOR{userId}@EXAMPLE.COM",
            FirstName = "Test",
            LastName = $"Instructor{userId}"
        });

        var instructor = new Instructor
        {
            Id = id,
            UserId = userId,
            DepartmentId = departmentId,
            Title = "Dr.",
            MaxWeeklyHours = 12
        };

        context.Instructors.Add(instructor);
        context.SaveChanges();
        return instructor;
    }

    private static Student AddStudent(AppDbContext context, int id)
    {
        context.Users.Add(new ApplicationUser
        {
            Id = 2000 + id,
            UserName = $"student{id}",
            NormalizedUserName = $"STUDENT{id}",
            Email = $"student{id}@example.com",
            NormalizedEmail = $"STUDENT{id}@EXAMPLE.COM",
            FirstName = "Test",
            LastName = $"Student{id}"
        });

        var student = new Student
        {
            Id = id,
            UserId = 2000 + id,
            StudentNumber = $"2025{id:0000}",
            NationalId = $"99999999{id:000}",
            Status = StudentStatus.Active,
            Cgpa = 0,
            Sgpa = 0,
            CompletedSemesters = 2,
            FrozenSemesterCount = 0,
            WithdrawalCount = 0,
            IsScholarshipStudent = false,
            HasFinancialBlock = false
        };

        context.Students.Add(student);
        context.SaveChanges();
        return student;
    }

    private static CourseSection AddSection(
        AppDbContext context,
        int id,
        int courseId,
        int capacity = 40,
        int enrolledCount = 0,
        bool isActive = true,
        int sectionNumber = 1)
    {
        var section = new CourseSection
        {
            Id = id,
            CourseId = courseId,
            TermId = TermId,
            Capacity = capacity,
            EnrolledCount = enrolledCount,
            SectionNumber = sectionNumber,
            Status = isActive ? CourseSectionStatus.Active : CourseSectionStatus.Draft,
            IsFinalized = false
        };

        context.CourseSections.Add(section);
        context.SaveChanges();
        return section;
    }

    private static void AddRegistration(
        AppDbContext context,
        int id,
        int studentId,
        int sectionId,
        RegistrationStatus status)
    {
        context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = id,
            StudentId = studentId,
            CourseSectionId = sectionId,
            Status = status,
            CreatedAt = DateTime.UtcNow
        });

        context.SaveChanges();
    }

    private static void AddAccessPolicy(AppDbContext context, int courseId, int departmentId)
    {
        context.CourseAccessPolicies.Add(new CourseAccessPolicy
        {
            CourseId = courseId,
            TermId = TermId,
            DepartmentId = departmentId
        });

        context.SaveChanges();
    }

    // Faz 3.C sonrası: tek monolitik CourseOpeningService 4 ayrı servise bölündü.
    // Test'lerde çoğu metod LifecycleService'tedir; sadece GetCurriculumCoursesForOpeningAsync
    // QueryService'tedir. CreateService default olarak Lifecycle döner (geriye dönük uyum).

    private static CourseSectionLifecycleService CreateService(AppDbContext context, IEnrollmentForecastService? forecastService = null)
    {
        _ = forecastService; // Lifecycle servisi forecast kullanmıyor; parametre query yardımcı testleri için tutuldu
        var cacheInvalidator = Mock.Of<IEnrollmentForecastCacheInvalidator>();
        var systemConfig = Mock.Of<ISystemConfigProvider>();
        var helpers = new CourseOpeningHelpers(context);
        return new CourseSectionLifecycleService(context, cacheInvalidator, systemConfig, helpers);
    }

    private static CourseOpeningQueryService CreateQueryService(AppDbContext context, IEnrollmentForecastService? forecastService = null)
    {
        forecastService ??= Mock.Of<IEnrollmentForecastService>();
        var helpers = new CourseOpeningHelpers(context);
        return new CourseOpeningQueryService(context, forecastService, helpers);
    }
}
