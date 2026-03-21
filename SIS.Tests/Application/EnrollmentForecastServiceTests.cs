using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class EnrollmentForecastServiceTests
{
    private const int FacultyId = 1;
    private const int DepartmentId = 1;
    private const int OtherDepartmentId = 2;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;
    private const int ActiveTermId = 1;
    private const int PastTermId = 2;
    private const int ClassroomId = 1;
    private const int SmallClassroomId = 2;

    [Fact]
    public async Task GetCourseDemandForecast_NormalEligibleDemand_ReturnsExpectedCounts()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var prerequisite = AddCourse(context, 100, "MATH201", "Calculus II", 6);
        var targetCourse = AddCourse(context, 101, "CENG301", "Software Engineering", 6);
        AddCurriculumCourse(context, prerequisite.Id, 2);
        AddCurriculumCourse(context, targetCourse.Id, 3);
        AddPrerequisite(context, targetCourse.Id, prerequisite.Id);

        var student = AddStudent(context, 1, completedSemesters: 2);
        var prerequisitePastSection = AddSection(context, 1000, prerequisite.Id, PastTermId);
        AddRegistration(context, 1, student.Id, prerequisitePastSection.Id, RegistrationStatus.Completed, GradeCode.CC);
        AddSection(context, 1001, targetCourse.Id, ActiveTermId, capacity: 40, classroomId: ClassroomId);

        var service = CreateService(context, student.Id, 3.10m);

        var result = await service.GetCourseDemandForecastAsync(targetCourse.Id);

        result.Should().NotBeNull();
        result!.PredictedEligibleCount.Should().Be(1);
        result.PredictedRetakeCount.Should().Be(0);
        result.PredictedOverloadOrUpperClassCount.Should().Be(0);
        result.TotalPredictedDemand.Should().Be(1);
    }

    [Fact]
    public async Task GetCourseDemandForecast_FailedGradesIncludingU_CountsAsRetake()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var targetCourse = AddCourse(context, 110, "CENG256", "Numerical Methods", 6);
        AddCurriculumCourse(context, targetCourse.Id, 4);
        AddSection(context, 1100, targetCourse.Id, ActiveTermId, capacity: 40, classroomId: ClassroomId);

        var student1 = AddStudent(context, 1, completedSemesters: 4);
        var student2 = AddStudent(context, 2, completedSemesters: 4);
        var student3 = AddStudent(context, 3, completedSemesters: 4);

        var section1 = AddSection(context, 1101, targetCourse.Id, PastTermId);
        var section2 = AddSection(context, 1102, targetCourse.Id, PastTermId);
        var section3 = AddSection(context, 1103, targetCourse.Id, PastTermId);

        AddRegistration(context, 1, student1.Id, section1.Id, RegistrationStatus.Completed, GradeCode.FF);
        AddRegistration(context, 2, student2.Id, section2.Id, RegistrationStatus.Completed, GradeCode.NA);
        AddRegistration(context, 3, student3.Id, section3.Id, RegistrationStatus.Completed, GradeCode.U);

        var service = CreateService(context, new Dictionary<int, decimal>
        {
            [student1.Id] = 2.10m,
            [student2.Id] = 2.10m,
            [student3.Id] = 2.10m
        });

        var result = await service.GetCourseDemandForecastAsync(targetCourse.Id);

        result.Should().NotBeNull();
        result!.PredictedRetakeCount.Should().Be(3);
        result.TotalPredictedDemand.Should().Be(3);
        result.Notes.Should().Contain(note => note.Contains("U notu", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetCourseDemandForecast_EctsLimitPreventsLateRegularCourseFromBeingSelected()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var student = AddStudent(context, 1, completedSemesters: 2);

        var regularCourses = new[]
        {
            AddCourse(context, 201, "AAA301", "Course A", 6),
            AddCourse(context, 202, "BBB301", "Course B", 6),
            AddCourse(context, 203, "CCC301", "Course C", 6),
            AddCourse(context, 204, "DDD301", "Course D", 6),
            AddCourse(context, 205, "EEE301", "Course E", 6),
            AddCourse(context, 206, "ZZZ301", "Target Course", 6)
        };

        foreach (var course in regularCourses)
        {
            AddCurriculumCourse(context, course.Id, 3);
        }

        AddSection(context, 2000, regularCourses[^1].Id, ActiveTermId, capacity: 40, classroomId: ClassroomId);

        var service = CreateService(context, student.Id, 1.80m);

        var result = await service.GetCourseDemandForecastAsync(regularCourses[^1].Id);

        result.Should().NotBeNull();
        result!.PredictedEligibleCount.Should().Be(0);
        result.TotalPredictedDemand.Should().Be(0);
    }

    [Fact]
    public async Task GetCourseDemandForecast_UpperClassCourseWithHeadroom_IsCountedInUpperBucket()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var prerequisite = AddCourse(context, 300, "MATH201", "Discrete Mathematics", 6);
        var regular1 = AddCourse(context, 301, "CENG213", "Data Structures", 6);
        var regular2 = AddCourse(context, 302, "CENG223", "Logic Design", 6);
        var regular3 = AddCourse(context, 303, "CENG241", "Statistics", 6);
        var upperTarget = AddCourse(context, 304, "CENG501", "Advanced Systems", 6);

        AddCurriculumCourse(context, prerequisite.Id, 2);
        AddCurriculumCourse(context, regular1.Id, 3);
        AddCurriculumCourse(context, regular2.Id, 3);
        AddCurriculumCourse(context, regular3.Id, 3);
        AddCurriculumCourse(context, upperTarget.Id, 5);
        AddPrerequisite(context, upperTarget.Id, prerequisite.Id);

        var student = AddStudent(context, 1, completedSemesters: 2);
        var prerequisitePastSection = AddSection(context, 3000, prerequisite.Id, PastTermId);
        AddRegistration(context, 1, student.Id, prerequisitePastSection.Id, RegistrationStatus.Completed, GradeCode.BB);
        AddSection(context, 3001, upperTarget.Id, ActiveTermId, capacity: 40, classroomId: ClassroomId);

        var service = CreateService(context, student.Id, 3.25m);

        var result = await service.GetCourseDemandForecastAsync(upperTarget.Id);

        result.Should().NotBeNull();
        result!.PredictedEligibleCount.Should().Be(0);
        result.PredictedRetakeCount.Should().Be(0);
        result.PredictedOverloadOrUpperClassCount.Should().Be(1);
        result.TotalPredictedDemand.Should().Be(1);
    }

    [Fact]
    public async Task GetCourseDemandForecast_UsesEffectiveCapacityForRecommendedSectionCount()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var targetCourse = AddCourse(context, 400, "CENG213", "Algorithms", 6);
        AddCurriculumCourse(context, targetCourse.Id, 3);
        AddSection(context, 4000, targetCourse.Id, ActiveTermId, capacity: 3, classroomId: SmallClassroomId);

        var student1 = AddStudent(context, 1, completedSemesters: 2);
        var student2 = AddStudent(context, 2, completedSemesters: 2);
        var student3 = AddStudent(context, 3, completedSemesters: 2);

        var service = CreateService(context, new Dictionary<int, decimal>
        {
            [student1.Id] = 2.80m,
            [student2.Id] = 2.80m,
            [student3.Id] = 2.80m
        });

        var result = await service.GetCourseDemandForecastAsync(targetCourse.Id);

        result.Should().NotBeNull();
        result!.PredictedEligibleCount.Should().Be(3);
        result.TotalPredictedDemand.Should().Be(3);
        result.RecommendedSectionCount.Should().Be(2);
        result.CapacityBasis.Should().Be("Aktif donem course section ortalamasi");
    }

    [Fact]
    public async Task GetCourseDemandForecast_WithoutEligibleData_ReturnsZeroDemand()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var targetCourse = AddCourse(context, 500, "CENG999", "Special Topics", 6);
        AddCurriculumCourse(context, targetCourse.Id, 7);

        var service = CreateService(context, new Dictionary<int, decimal>());

        var result = await service.GetCourseDemandForecastAsync(targetCourse.Id);

        result.Should().NotBeNull();
        result!.PredictedEligibleCount.Should().Be(0);
        result.PredictedRetakeCount.Should().Be(0);
        result.PredictedOverloadOrUpperClassCount.Should().Be(0);
        result.TotalPredictedDemand.Should().Be(0);
        result.Notes.Should().Contain(note => note.Contains("uygun aktif ogrenci", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetCourseDemandForecast_DoesNotDoubleCountSameStudentAcrossBuckets()
    {
        using var context = CreateContext();
        SeedBaseData(context);

        var targetCourse = AddCourse(context, 600, "CENG256", "Microprocessors", 6);
        AddCurriculumCourse(context, targetCourse.Id, 4);

        var student = AddStudent(context, 1, completedSemesters: 4);
        var pastSection = AddSection(context, 6000, targetCourse.Id, PastTermId);
        var activeSection = AddSection(context, 6001, targetCourse.Id, ActiveTermId, capacity: 40, classroomId: ClassroomId);

        AddRegistration(context, 1, student.Id, pastSection.Id, RegistrationStatus.Completed, GradeCode.FF);
        AddRegistration(context, 2, student.Id, activeSection.Id, RegistrationStatus.Active);

        var service = CreateService(context, student.Id, 2.40m);

        var result = await service.GetCourseDemandForecastAsync(targetCourse.Id);

        result.Should().NotBeNull();
        result!.PredictedRetakeCount.Should().Be(1);
        result.PredictedEligibleCount.Should().Be(0);
        result.PredictedOverloadOrUpperClassCount.Should().Be(0);
        result.TotalPredictedDemand.Should().Be(1);
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
        context.Faculties.Add(new Faculty
        {
            Id = FacultyId,
            Name = "Engineering",
            Code = "ENG"
        });

        context.Departments.AddRange(
            new Department
            {
                Id = DepartmentId,
                Name = "Computer Engineering",
                Code = "CENG",
                FacultyId = FacultyId
            },
            new Department
            {
                Id = OtherDepartmentId,
                Name = "Electrical Engineering",
                Code = "EEE",
                FacultyId = FacultyId
            });

        context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId,
            Name = "Computer Engineering",
            Code = "CENG-BSC",
            DepartmentId = DepartmentId,
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240
        });

        context.Curricula.Add(new Curriculum
        {
            Id = CurriculumId,
            ProgramId = ProgramId,
            Version = "2025",
            IsActive = true
        });

        context.AcademicTerms.AddRange(
            new AcademicTerm
            {
                Id = ActiveTermId,
                Name = "2025-2026 Fall",
                Year = 2025,
                Semester = SemesterType.Fall,
                RegistrationStart = DateTime.UtcNow.AddDays(-7),
                RegistrationEnd = DateTime.UtcNow.AddDays(7),
                AddDropStart = DateTime.UtcNow.AddDays(8),
                AddDropEnd = DateTime.UtcNow.AddDays(14),
                WithdrawalDeadlineWeek = 7,
                TermStart = DateTime.UtcNow.AddDays(-14),
                TermEnd = DateTime.UtcNow.AddDays(90),
                IsActive = true
            },
            new AcademicTerm
            {
                Id = PastTermId,
                Name = "2024-2025 Spring",
                Year = 2024,
                Semester = SemesterType.Spring,
                RegistrationStart = DateTime.UtcNow.AddDays(-200),
                RegistrationEnd = DateTime.UtcNow.AddDays(-190),
                AddDropStart = DateTime.UtcNow.AddDays(-189),
                AddDropEnd = DateTime.UtcNow.AddDays(-180),
                WithdrawalDeadlineWeek = 7,
                TermStart = DateTime.UtcNow.AddDays(-210),
                TermEnd = DateTime.UtcNow.AddDays(-120),
                IsActive = false
            });

        context.Classrooms.AddRange(
            new Classroom
            {
                Id = ClassroomId,
                Name = "A-101",
                Capacity = 40,
                Type = ClassroomType.Lecture
            },
            new Classroom
            {
                Id = SmallClassroomId,
                Name = "B-202",
                Capacity = 2,
                Type = ClassroomType.Lecture
            });

        context.SaveChanges();
    }

    private static Student AddStudent(
        AppDbContext context,
        int id,
        int completedSemesters,
        StudentStatus status = StudentStatus.Active,
        bool hasFinancialBlock = false)
    {
        var student = new Student
        {
            Id = id,
            UserId = 1000 + id,
            StudentNumber = $"2025{id:0000}",
            NationalId = $"99999999{id:000}",
            Status = status,
            Cgpa = 0,
            Sgpa = 0,
            CompletedSemesters = completedSemesters,
            FrozenSemesterCount = 0,
            WithdrawalCount = 0,
            IsScholarshipStudent = false,
            HasFinancialBlock = hasFinancialBlock
        };

        context.Students.Add(student);
        context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = student.Id,
            ProgramId = ProgramId,
            CurriculumId = CurriculumId,
            Type = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow.AddYears(-2),
            IsActive = true
        });

        context.SaveChanges();
        return student;
    }

    private static Course AddCourse(
        AppDbContext context,
        int id,
        string code,
        string name,
        int ects,
        int? departmentId = DepartmentId)
    {
        var course = new Course
        {
            Id = id,
            Code = code,
            Name = name,
            Ects = ects,
            TheoryHours = 3,
            LabHours = 0,
            IsNonGpa = false,
            DepartmentId = departmentId
        };

        context.Courses.Add(course);
        context.SaveChanges();
        return course;
    }

    private static void AddCurriculumCourse(AppDbContext context, int courseId, int suggestedSemester, bool isCompulsory = true)
    {
        context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId,
            CourseId = courseId,
            SuggestedSemester = suggestedSemester,
            IsCompulsory = isCompulsory
        });

        context.SaveChanges();
    }

    private static void AddPrerequisite(AppDbContext context, int courseId, int requiredCourseId)
    {
        context.CoursePrerequisites.Add(new CoursePrerequisite
        {
            CourseId = courseId,
            RequiredCourseId = requiredCourseId
        });

        context.SaveChanges();
    }

    private static CourseSection AddSection(
        AppDbContext context,
        int id,
        int courseId,
        int termId,
        int capacity = 40,
        int enrolledCount = 0,
        int? classroomId = null,
        bool isActive = true)
    {
        var section = new CourseSection
        {
            Id = id,
            CourseId = courseId,
            TermId = termId,
            Capacity = capacity,
            EnrolledCount = enrolledCount,
            ClassroomId = classroomId,
            IsActive = isActive,
            ScheduleJson = "[]"
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
        RegistrationStatus status,
        GradeCode? grade = null)
    {
        context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = id,
            StudentId = studentId,
            CourseSectionId = sectionId,
            Status = status,
            Grade = grade,
            CreatedAt = DateTime.UtcNow
        });

        context.SaveChanges();
    }

    private static EnrollmentForecastService CreateService(
        AppDbContext context,
        int studentId,
        decimal cgpa)
    {
        return CreateService(context, new Dictionary<int, decimal> { [studentId] = cgpa });
    }

    private static EnrollmentForecastService CreateService(
        AppDbContext context,
        IReadOnlyDictionary<int, decimal> cgpasByStudentId)
    {
        var gpaServiceMock = new Mock<IGpaCalculationService>();
        gpaServiceMock
            .Setup(service => service.CalculateCgpaAsync(It.IsAny<int>()))
            .ReturnsAsync((int studentId) => cgpasByStudentId.TryGetValue(studentId, out var cgpa) ? cgpa : 0m);

        return new EnrollmentForecastService(context, gpaServiceMock.Object);
    }
}
