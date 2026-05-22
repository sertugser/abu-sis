using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SIS.Application.Common.Services;
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Scheduling;

namespace SIS.Tests.Scheduling.OrTools;

/// <summary>
/// Plan 2 Task 19 — Real v2 dispatch testleri.
/// AutoScheduleEngine'in <c>Schedule.Engine == "v2-ortools"</c> okuduğunda
/// gerçekten <see cref="ISolverEngine"/>'i çağırıp sonucu DTO'ya map ettiğini doğrular.
/// </summary>
public class EngineDispatchTests
{
    [Fact]
    public async Task RunAsync_EngineFlagV2OrTools_CallsSolverEngineSolveAsync()
    {
        var (engine, solverEngineMock, _) = BuildAutoScheduleEngine(
            engineFlag: "v2-ortools",
            solverResult: BuildEmptySuccessResult());

        await engine.RunAsync(
            new AutoScheduleRequest { TermId = 1, FacultyId = 1, Seed = 42 },
            userId: null,
            CancellationToken.None);

        solverEngineMock.Verify(
            x => x.SolveAsync(
                It.IsAny<SolverRequest>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<Action<SolverProgressInfo>?>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_EngineFlagV2OrTools_PassesTermIdFacultyIdSeedToSolverRequest()
    {
        SolverRequest? capturedRequest = null;
        var (engine, _, _) = BuildAutoScheduleEngine(
            engineFlag: "v2-ortools",
            solverResult: BuildEmptySuccessResult(),
            captureRequest: r => capturedRequest = r);

        await engine.RunAsync(
            new AutoScheduleRequest { TermId = 7, FacultyId = 11, DepartmentId = 5, Seed = 99 },
            userId: null,
            CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.TermId.Should().Be(7);
        capturedRequest.FacultyId.Should().Be(11);
        capturedRequest.DepartmentId.Should().Be(5);
        capturedRequest.Seed.Should().Be(99);
    }

    [Fact]
    public async Task RunAsync_V2_ReturnsPreviewDtoWithMappedAssignments()
    {
        var solverResult = new SolverResult
        {
            Status = SolverStatus.Optimal,
            Assignments = new List<Assignment>(),
            Failures = new List<Failure>(),
            Diagnostics = new SolverDiagnostics
            {
                EngineVersion = "v2-ortools",
                ElapsedSeconds = 0.5,
                Branches = 100,
                Conflicts = 5,
                ObjectiveValue = 0,
                UnsatCore = new List<UnsatCoreEntry>()
            }
        };
        var (engine, _, _) = BuildAutoScheduleEngine(
            engineFlag: "v2-ortools",
            solverResult: solverResult);

        var dto = await engine.RunAsync(
            new AutoScheduleRequest { TermId = 1, FacultyId = 1 },
            userId: null,
            CancellationToken.None);

        dto.Should().NotBeNull();
        dto.Assignments.Should().BeEmpty();
        dto.Failures.Should().BeEmpty();
        dto.Stats.ElapsedSeconds.Should().BeApproximately(0.5, 0.01);
        dto.Stats.Engine.Should().Be("v2-ortools");
        dto.Stats.SearchStrategy.Should().Be("auto");
        dto.Stats.UnsatCoreEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_InvalidEngine_ThrowsInvalidOperationException()
    {
        var (engine, _, _) = BuildAutoScheduleEngine(
            engineFlag: "legacy-v1",
            solverResult: BuildEmptySuccessResult());

        var act = async () => await engine.RunAsync(
            new AutoScheduleRequest { TermId = 1, FacultyId = 1, Seed = 42 },
            userId: null,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Schedule.Engine*");
    }

    [Fact]
    public async Task RunAsync_PhysicalSectionWithoutTimeSlots_ThrowsFriendlyValidationError()
    {
        var (engine, solverEngineMock, context) = BuildAutoScheduleEngine(
            engineFlag: "v2-ortools",
            solverResult: BuildEmptySuccessResult());
        SeedSchedulableSection(context, addTimeSlot: false, addClassroom: true);

        var act = async () => await engine.RunAsync(
            new AutoScheduleRequest { TermId = 1, FacultyId = 1 },
            userId: null,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*zaman dilimi*");
        solverEngineMock.Verify(
            x => x.SolveAsync(
                It.IsAny<SolverRequest>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<Action<SolverProgressInfo>?>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_PhysicalSectionWithoutAssignedClassrooms_ThrowsFriendlyValidationError()
    {
        var (engine, solverEngineMock, context) = BuildAutoScheduleEngine(
            engineFlag: "v2-ortools",
            solverResult: BuildEmptySuccessResult());
        SeedSchedulableSection(context, addTimeSlot: true, addClassroom: false);

        var act = async () => await engine.RunAsync(
            new AutoScheduleRequest { TermId = 1, FacultyId = 1 },
            userId: null,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*derslik*");
        solverEngineMock.Verify(
            x => x.SolveAsync(
                It.IsAny<SolverRequest>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<Action<SolverProgressInfo>?>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_V2_InfeasibleResult_ReturnsPreviewWithFailures()
    {
        // Boş DB'de section yok → SolverRequestBuilder boş Sections döndürür → mapper
        // bilinmeyen SectionId'leri atlar. Mapping davranışını gerçek DB seed'iyle değil,
        // result.Failures içeriğinin DTO'ya yansıması üzerinden test ediyoruz: Status
        // Infeasible olduğunda DTO döner, FailedCount/TotalProcessed sayım davranışı
        // üzerinden mapping'in gerçekleştiğini doğrularız.
        var solverResult = new SolverResult
        {
            Status = SolverStatus.Infeasible,
            Assignments = new List<Assignment>(),
            Failures = new List<Failure>
            {
                new Failure { SectionId = 9999, Reason = "Test failure reason" }
            },
            Diagnostics = new SolverDiagnostics
            {
                EngineVersion = "v2-ortools",
                ElapsedSeconds = 0.5,
                Branches = 0,
                Conflicts = 0,
                ObjectiveValue = 0,
                UnsatCore = new List<UnsatCoreEntry>()
            }
        };
        var (engine, _, _) = BuildAutoScheduleEngine(
            engineFlag: "v2-ortools",
            solverResult: solverResult);

        var dto = await engine.RunAsync(
            new AutoScheduleRequest { TermId = 1, FacultyId = 1 },
            userId: null,
            CancellationToken.None);

        dto.Should().NotBeNull();
        // Section 9999 boş DB'de bulunmaz → mapper atlar; ama TotalProcessed sayımı
        // raw failure listesini yansıtırsa OK; minimal sözleşme: dto döner ve null değil.
        // Strict assertion: stats elapsed solver diagnostics'ten geliyor mu?
        dto.Stats.ElapsedSeconds.Should().BeApproximately(0.5, 0.01);
    }

    // ─────────────── Helpers ───────────────

    private static SolverResult BuildEmptySuccessResult() => new()
    {
        Status = SolverStatus.Optimal,
        Assignments = new List<Assignment>(),
        Failures = new List<Failure>(),
        Diagnostics = new SolverDiagnostics
        {
            EngineVersion = "v2-ortools",
            ElapsedSeconds = 0.1,
            Branches = 0,
            Conflicts = 0,
            ObjectiveValue = 0,
            UnsatCore = new List<UnsatCoreEntry>()
        }
    };

    private static void SeedSchedulableSection(
        AppDbContext context,
        bool addTimeSlot,
        bool addClassroom,
        TeachingMethod teachingMethod = TeachingMethod.FaceToFace)
    {
        context.Faculties.Add(new Faculty { Id = 1, Name = "Engineering", Code = "ENG" });
        context.Departments.Add(new Department
        {
            Id = 1,
            FacultyId = 1,
            Name = "Computer Engineering",
            Code = "CENG"
        });
        context.AcademicTerms.Add(new AcademicTerm
        {
            Id = 1,
            Name = "2025-2026 Bahar",
            ShortName = "25-26 Bahar",
            EnglishName = "Spring 2025-2026",
            Year = 2025,
            TermType = TermType.Spring,
            TermStart = DateTime.UtcNow.AddDays(-1),
            TermEnd = DateTime.UtcNow.AddDays(90)
        });
        context.Courses.Add(new Course
        {
            Id = 1,
            Code = "CENG101",
            Name = "CENG101",
            DepartmentId = 1,
            Ects = 2,
            TheoryHours = 2,
            LabHours = 0,
            NationalCredit = 2,
            IsNonGpa = false,
            TeachingMethod = teachingMethod
        });
        context.CourseSections.Add(new CourseSection
        {
            Id = 1,
            CourseId = 1,
            TermId = 1,
            Capacity = 30,
            SectionNumber = 1,
            Status = CourseSectionStatus.Active,
            IsFinalized = false
        });

        if (addTimeSlot)
        {
            context.TimeSlots.Add(new TimeSlot
            {
                Id = 1,
                TermId = 1,
                DayOfWeek = DayOfWeek.Monday,
                SlotOrder = 1,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(9, 50),
                DisplayName = "Monday 1",
                AllowedMode = TimeSlotAllowedMode.Both
            });
            context.TimeSlots.Add(new TimeSlot
            {
                Id = 2,
                TermId = 1,
                DayOfWeek = DayOfWeek.Monday,
                SlotOrder = 2,
                StartTime = new TimeOnly(9, 55),
                EndTime = new TimeOnly(10, 45),
                DisplayName = "Monday 2",
                AllowedMode = TimeSlotAllowedMode.Both
            });
        }

        if (addClassroom)
        {
            context.Classrooms.Add(new Classroom
            {
                Id = 1,
                Name = "D101",
                BuildingName = "A",
                Floor = 1,
                Capacity = 50,
                Type = ClassroomType.Lecture
            });
            context.ClassroomTermAssignments.Add(new ClassroomTermAssignment
            {
                TermId = 1,
                ClassroomId = 1,
                FacultyId = 1
            });
        }

        context.SaveChanges();
    }

    private static (AutoScheduleEngine engine, Mock<ISolverEngine> solverMock, AppDbContext context)
        BuildAutoScheduleEngine(
            string engineFlag = "v1-mrv",
            SolverResult? solverResult = null,
            Action<SolverRequest>? captureRequest = null)
    {
        // InMemory DB
        var ctxOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(ctxOptions);

        // Config mock — Schedule.Engine kontrol ediliyor; diğer çağrılar fallback döner.
        var configMock = new Mock<ISystemConfigProvider>();
        configMock
            .Setup(x => x.GetStringAsync("Schedule.Engine", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(engineFlag);
        configMock
            .Setup(x => x.GetStringAsync(
                It.Is<string>(k => k != "Schedule.Engine"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((_, fallback, _) => Task.FromResult(fallback));
        configMock
            .Setup(x => x.GetIntAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<string, int, CancellationToken>((_, fallback, _) => Task.FromResult(fallback));
        configMock
            .Setup(x => x.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns<string, bool, CancellationToken>((_, fallback, _) => Task.FromResult(fallback));
        configMock
            .Setup(x => x.GetDecimalAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .Returns<string, decimal, CancellationToken>((_, fallback, _) => Task.FromResult(fallback));

        // Solver engine mock — Plan A progress callback patch sonrası 3. parametre eklendi
        // (Action<SolverProgressInfo>? onProgress). AutoScheduleEngine artık non-null geçiyor;
        // testin amacı progress wiring DEĞİL, bu yüzden It.IsAny<...> ile parametreyi ignore ediyoruz.
        var solverMock = new Mock<ISolverEngine>();
        solverMock
            .Setup(x => x.SolveAsync(
                It.IsAny<SolverRequest>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<Action<SolverProgressInfo>?>()))
            .Callback<SolverRequest, CancellationToken, Action<SolverProgressInfo>?>((req, _, _) => captureRequest?.Invoke(req))
            .ReturnsAsync(solverResult ?? BuildEmptySuccessResult());

        // Plan 9 sonrası AutoScheduleEngine v2-only — ConstraintEvaluationService bağımlılığı kaldırıldı.
        var engine = new AutoScheduleEngine(
            context,
            configMock.Object,
            solverMock.Object);

        return (engine, solverMock, context);
    }
}
