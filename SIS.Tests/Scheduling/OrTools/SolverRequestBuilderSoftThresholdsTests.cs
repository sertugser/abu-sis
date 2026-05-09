using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Scheduling.OrTools;

namespace SIS.Tests.Scheduling.OrTools;

/// <summary>
/// Plan 8 Task 2 — SolverRequestBuilder SoftThresholds + CoEnrollmentPairs +
/// AdjacentYearSoftPairs data wiring testleri.
/// </summary>
public class SolverRequestBuilderSoftThresholdsTests
{
    // ────────────────────────────────────────────────────────────────────────
    // Test infra
    // ────────────────────────────────────────────────────────────────────────

    private static AppDbContext NewInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// Keyed-lookup ISystemConfigProvider double — int/string/bool/decimal değerleri
    /// dictionary'lerden döndürür, eksik key'lerde fallback'e geri düşer.
    /// </summary>
    private sealed class FakeSystemConfigProvider : ISystemConfigProvider
    {
        public Dictionary<string, int> Ints { get; } = new();
        public Dictionary<string, string> Strings { get; } = new();
        public Dictionary<string, bool> Bools { get; } = new();
        public Dictionary<string, decimal> Decimals { get; } = new();

        public Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
            => Task.FromResult(Ints.TryGetValue(key, out var v) ? v : fallback);

        public Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default)
            => Task.FromResult(Decimals.TryGetValue(key, out var v) ? v : fallback);

        public Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default)
            => Task.FromResult(Strings.TryGetValue(key, out var v) ? v : fallback);

        public Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default)
            => Task.FromResult(Bools.TryGetValue(key, out var v) ? v : fallback);

        public void InvalidateCache(string key) { /* no-op */ }
    }

    private static async Task<AppDbContext> SeedMinimalAsync()
    {
        var ctx = NewInMemoryContext();
        ctx.Faculties.Add(new Faculty { Id = 1, Code = "MUH", Name = "M" });
        ctx.Departments.Add(new Department { Id = 1, Code = "BM", Name = "BM", FacultyId = 1 });
        ctx.AcademicTerms.Add(new AcademicTerm
        {
            Id = 1,
            Year = 2026,
            TermType = TermType.Spring,
            Name = "S26",
            ShortName = "S26",
            EnglishName = "S26",
            ShortEnglishName = "S26",
            TermStart = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)
        });
        ctx.Courses.Add(new Course
        {
            Id = 100,
            Code = "X1",
            Name = "X1",
            DepartmentId = 1,
            TheoryHours = 3,
            LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace,
            Ects = 6,
            NationalCredit = 6
        });
        ctx.CourseSections.Add(new CourseSection
        {
            Id = 1000,
            CourseId = 100,
            TermId = 1,
            SectionNumber = 1,
            Capacity = 30,
            Status = CourseSectionStatus.Active,
            IsFinalized = false
        });
        ctx.Classrooms.Add(new Classroom
        {
            Id = 9,
            Name = "C",
            Capacity = 50,
            Type = ClassroomType.Lecture
        });
        ctx.ClassroomTermAssignments.Add(new ClassroomTermAssignment
        {
            Id = 1,
            TermId = 1,
            FacultyId = 1,
            ClassroomId = 9
        });
        await ctx.SaveChangesAsync();
        return ctx;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Tests
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_NoSystemConfig_UsesDefaults()
    {
        // Arrange — SystemConfig'te ilgili key yok ⇒ SoftThresholds default değerlere düşer.
        await using var ctx = await SeedMinimalAsync();
        var provider = new FakeSystemConfigProvider();
        var builder = new SolverRequestBuilder(ctx, provider);

        // Act
        var req = await builder.BuildAsync(termId: 1, facultyId: 1, departmentId: null, seed: null, default);

        // Assert — record default'ları
        var t = req.SoftThresholds;
        t.S1MinOccupancyPercent.Should().Be(60);
        t.S1MaxOccupancyPercent.Should().Be(85);
        t.S1Weight.Should().Be(1);
        t.S2MaxDailyHours.Should().Be(7); // Plan 9 Issue 5 — credit-hour cap (was 6)
        t.S2Weight.Should().Be(10);
        t.S3MaxSectionsPerSlot.Should().Be(5);
        t.S3Weight.Should().Be(5);
        t.S4MinCoPairThreshold.Should().Be(20);
        t.S4Weight.Should().Be(1);
        t.S5LunchBreakStart.Should().Be(new TimeOnly(11, 40));
        t.S5LunchBreakEnd.Should().Be(new TimeOnly(12, 20));
        t.S5Weight.Should().Be(8);
        t.S7MaxGapSlots.Should().Be(3);
        t.S7Weight.Should().Be(3);
        t.S8Weight.Should().Be(4);
        t.S12MaxImbalancePercent.Should().Be(20);
        t.S12Weight.Should().Be(2);
        t.S20PenaltyPerPair.Should().Be(5);
        t.S20Weight.Should().Be(1);
        t.DistDiscouragedWeight.Should().Be(50);
        t.DistStronglyDiscouragedWeight.Should().Be(200);
    }

    [Fact]
    public async Task BuildAsync_SystemConfigOverrides_AppliedToThresholds()
    {
        // Arrange — birkaç override değeri
        await using var ctx = await SeedMinimalAsync();
        var provider = new FakeSystemConfigProvider();
        provider.Ints["Classroom.OccupancyTarget.Min"] = 70;
        provider.Ints["Classroom.OccupancyTarget.Max"] = 90;
        provider.Ints["Schedule.S1Weight"] = 3;
        provider.Ints["SoftConstraint.S2.MaxDailyHours"] = 8;
        provider.Ints["Schedule.S2Weight"] = 15;
        provider.Ints["SoftConstraint.S4.MinCoPairThreshold"] = 25;
        provider.Strings["Schedule.LunchBreak.Start"] = "12:00";
        provider.Strings["Schedule.LunchBreak.End"] = "13:00";
        provider.Ints["Schedule.DistDiscouragedWeight"] = 100;
        provider.Ints["Schedule.DistStronglyDiscouragedWeight"] = 500;
        var builder = new SolverRequestBuilder(ctx, provider);

        // Act
        var req = await builder.BuildAsync(1, 1, null, null, default);

        // Assert
        var t = req.SoftThresholds;
        t.S1MinOccupancyPercent.Should().Be(70);
        t.S1MaxOccupancyPercent.Should().Be(90);
        t.S1Weight.Should().Be(3);
        t.S2MaxDailyHours.Should().Be(8);
        t.S2Weight.Should().Be(15);
        t.S4MinCoPairThreshold.Should().Be(25);
        t.S5LunchBreakStart.Should().Be(new TimeOnly(12, 0));
        t.S5LunchBreakEnd.Should().Be(new TimeOnly(13, 0));
        t.DistDiscouragedWeight.Should().Be(100);
        t.DistStronglyDiscouragedWeight.Should().Be(500);
    }

    [Fact]
    public async Task BuildAsync_NoCourseRegistrations_CoEnrollmentPairsEmpty()
    {
        // Arrange — DB'de hiç CourseRegistration yok
        await using var ctx = await SeedMinimalAsync();
        var provider = new FakeSystemConfigProvider();
        var builder = new SolverRequestBuilder(ctx, provider);

        // Act
        var req = await builder.BuildAsync(1, 1, null, null, default);

        // Assert
        req.CoEnrollmentPairs.Should().BeEmpty(
            because: "CourseRegistrations boş ⇒ pair üretimi yok");
    }

    [Fact]
    public async Task BuildAsync_CourseRegistrationsAboveThreshold_PairsLoaded()
    {
        // Arrange — geçmiş 1 dönemde 2 öğrenci 2 ders almış (CENG101 + CENG102),
        // threshold = 2 ⇒ pair yüklenir.
        await using var ctx = await SeedMinimalAsync();

        // currentTermId = 5 olacak; 1..4 geçmiş dönemler. SeedMinimalAsync term Id=1 zaten var.
        // currentTermId'yi yükseltmek için yeni term ekleriz (Id=5).
        ctx.AcademicTerms.AddRange(
            new AcademicTerm
            {
                Id = 2,
                Year = 2025,
                TermType = TermType.Spring,
                Name = "Sp25",
                ShortName = "Sp25",
                EnglishName = "Sp25",
                ShortEnglishName = "Sp25",
                TermStart = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                TermEnd = new DateTime(2025, 9, 30, 0, 0, 0, DateTimeKind.Utc)
            },
            new AcademicTerm
            {
                Id = 5,
                Year = 2027,
                TermType = TermType.Fall,
                Name = "F27",
                ShortName = "F27",
                EnglishName = "F27",
                ShortEnglishName = "F27",
                TermStart = new DateTime(2027, 9, 15, 0, 0, 0, DateTimeKind.Utc),
                TermEnd = new DateTime(2028, 1, 15, 0, 0, 0, DateTimeKind.Utc)
            });

        ctx.Courses.Add(new Course
        {
            Id = 200,
            Code = "X2",
            Name = "X2",
            DepartmentId = 1,
            TheoryHours = 3,
            LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace,
            Ects = 6,
            NationalCredit = 6
        });

        // Geçmiş dönem section'ları (TermId=2 — Spring 2025)
        ctx.CourseSections.Add(new CourseSection
        {
            Id = 9000,
            CourseId = 100,
            TermId = 2,
            SectionNumber = 1,
            Capacity = 50,
            Status = CourseSectionStatus.Active,
            IsFinalized = true
        });
        ctx.CourseSections.Add(new CourseSection
        {
            Id = 9001,
            CourseId = 200,
            TermId = 2,
            SectionNumber = 1,
            Capacity = 50,
            Status = CourseSectionStatus.Active,
            IsFinalized = true
        });

        // 2 öğrenci × 2 ders = 4 registration
        ctx.CourseRegistrations.AddRange(
            new CourseRegistration { Id = 1, StudentId = 1, CourseSectionId = 9000, Status = RegistrationStatus.Active },
            new CourseRegistration { Id = 2, StudentId = 1, CourseSectionId = 9001, Status = RegistrationStatus.Active },
            new CourseRegistration { Id = 3, StudentId = 2, CourseSectionId = 9000, Status = RegistrationStatus.Active },
            new CourseRegistration { Id = 4, StudentId = 2, CourseSectionId = 9001, Status = RegistrationStatus.Active }
        );
        await ctx.SaveChangesAsync();

        var provider = new FakeSystemConfigProvider();
        // Threshold = 2 (default 20'yi düşür)
        provider.Ints["SoftConstraint.S4.MinCoPairThreshold"] = 2;
        var builder = new SolverRequestBuilder(ctx, provider);

        // Act — currentTermId=5; recentTermIds = {2, 1} (id < 5, en yeni 3 desc).
        var req = await builder.BuildAsync(termId: 5, facultyId: 1, departmentId: null, seed: null, default);

        // Assert
        req.CoEnrollmentPairs.Should().HaveCount(1);
        var pair = req.CoEnrollmentPairs[0];
        pair.CourseId1.Should().Be(100, because: "canonical: smaller course id first");
        pair.CourseId2.Should().Be(200);
        pair.StudentCount.Should().Be(2, because: "2 öğrenci aynı 2 dersi aldı");
    }
}
