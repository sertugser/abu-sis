using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using System.Collections.Generic;

namespace SIS.Tests.Application;

public class ElectivePoolProgressCalculatorTests
{
    private static AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static readonly IReadOnlyDictionary<int, int?> EmptyOverrides =
        new Dictionary<int, int?>();

    /// <summary>
    /// Adds a CourseRegistration to the in-memory context.
    /// <paramref name="termId"/> lets tests control the term for grade-visibility checks.
    /// </summary>
    private static void AddReg(AppDbContext ctx, int id, int studentId, int poolId, int courseId,
        RegistrationStatus status, GradeCode? grade, int ects, int termId = 1, int? ectsSnapshot = null)
    {
        if (!ctx.Courses.Any(c => c.Id == courseId))
            ctx.Courses.Add(new Course { Id = courseId, Code = $"C{courseId}", Name = "c", Ects = ects });

        if (!ctx.CourseSections.Any(cs => cs.Id == id))
            ctx.CourseSections.Add(new CourseSection { Id = id, CourseId = courseId, TermId = termId, Status = CourseSectionStatus.Active });

        ctx.CourseRegistrations.Add(new CourseRegistration
        {
            Id = id, StudentId = studentId, CourseSectionId = id,
            AppliedElectivePoolId = poolId, Status = status, Grade = grade,
            EctsSnapshot = ectsSnapshot ?? ects
        });
    }

    // ─── Existing tests (updated to pass poolEctsPerCourse) ─────────────────────

    [Fact]
    public async Task Calculate_CountsOnlyPassed_ExcludesDraftAndFailedAndWithdrawn()
    {
        using var ctx = CreateContext();
        AddReg(ctx, 1, 5, 100, 1, RegistrationStatus.Active, GradeCode.BA, 6);
        AddReg(ctx, 2, 5, 100, 2, RegistrationStatus.Draft, null, 6);
        AddReg(ctx, 3, 5, 100, 3, RegistrationStatus.Active, GradeCode.FF, 6);
        AddReg(ctx, 4, 5, 100, 4, RegistrationStatus.Withdrawn, GradeCode.W, 6);
        await ctx.SaveChangesAsync();
        var calc = new ElectivePoolProgressCalculator(ctx);

        var result = await calc.CalculateAsync(5, new[] { 100 }, EmptyOverrides);

        result[100].CompletedCourseCount.Should().Be(1);
        result[100].EarnedEcts.Should().Be(6);
    }

    [Fact]
    public async Task Calculate_ReturnsZero_ForPoolWithNoPassedRegs()
    {
        using var ctx = CreateContext();
        AddReg(ctx, 1, 5, 200, 1, RegistrationStatus.Draft, null, 5);
        await ctx.SaveChangesAsync();
        var calc = new ElectivePoolProgressCalculator(ctx);

        var result = await calc.CalculateAsync(5, new[] { 200 }, EmptyOverrides);

        result[200].CompletedCourseCount.Should().Be(0);
        result[200].EarnedEcts.Should().Be(0);
    }

    // ─── Fix 1: Dropped excluded ─────────────────────────────────────────────────

    [Fact]
    public async Task Calculate_ExcludesDropped_EvenWithPassableGrade()
    {
        using var ctx = CreateContext();
        // Dropped registration with a passing grade — must be excluded
        AddReg(ctx, 1, 5, 300, 1, RegistrationStatus.Dropped, GradeCode.BB, 6);
        // Active passing registration for control
        AddReg(ctx, 2, 5, 300, 2, RegistrationStatus.Active, GradeCode.AA, 6);
        await ctx.SaveChangesAsync();
        var calc = new ElectivePoolProgressCalculator(ctx);

        var result = await calc.CalculateAsync(5, new[] { 300 }, EmptyOverrides);

        result[300].CompletedCourseCount.Should().Be(1, "Dropped regs must be excluded regardless of grade");
        result[300].EarnedEcts.Should().Be(6);
    }

    // ─── Fix 1: Failing grades excluded (NA, U, I) ───────────────────────────────

    [Theory]
    [InlineData(GradeCode.NA)]
    [InlineData(GradeCode.U)]
    [InlineData(GradeCode.I)]
    public async Task Calculate_ExcludesFailingGrades(GradeCode failGrade)
    {
        using var ctx = CreateContext();
        AddReg(ctx, 1, 5, 400, 1, RegistrationStatus.Active, failGrade, 6);
        await ctx.SaveChangesAsync();
        var calc = new ElectivePoolProgressCalculator(ctx);

        var result = await calc.CalculateAsync(5, new[] { 400 }, EmptyOverrides);

        result[400].CompletedCourseCount.Should().Be(0, $"{failGrade} must not count as passed");
        result[400].EarnedEcts.Should().Be(0);
    }

    // ─── Fix 3: ECTS fallback chain ──────────────────────────────────────────────

    [Fact]
    public async Task Calculate_UsesEctsSnapshot_WhenProvided()
    {
        using var ctx = CreateContext();
        // EctsSnapshot = 5, Course.Ects = 6, poolOverride = 7 → snapshot wins
        ctx.Courses.Add(new Course { Id = 1, Code = "C1", Name = "c", Ects = 6 });
        ctx.CourseSections.Add(new CourseSection { Id = 1, CourseId = 1, TermId = 1, Status = CourseSectionStatus.Active });
        ctx.CourseRegistrations.Add(new CourseRegistration
        {
            Id = 1, StudentId = 5, CourseSectionId = 1,
            AppliedElectivePoolId = 500, Status = RegistrationStatus.Active,
            Grade = GradeCode.BB, EctsSnapshot = 5
        });
        await ctx.SaveChangesAsync();
        var poolOverrides = new Dictionary<int, int?> { [500] = 7 };
        var calc = new ElectivePoolProgressCalculator(ctx);

        var result = await calc.CalculateAsync(5, new[] { 500 }, poolOverrides);

        result[500].EarnedEcts.Should().Be(5, "EctsSnapshot takes highest priority");
    }

    [Fact]
    public async Task Calculate_UsesPoolEctsOverride_WhenSnapshotIsNull()
    {
        using var ctx = CreateContext();
        // EctsSnapshot = null, poolOverride = 7, Course.Ects = 6 → pool override wins
        ctx.Courses.Add(new Course { Id = 1, Code = "C1", Name = "c", Ects = 6 });
        ctx.CourseSections.Add(new CourseSection { Id = 1, CourseId = 1, TermId = 1, Status = CourseSectionStatus.Active });
        ctx.CourseRegistrations.Add(new CourseRegistration
        {
            Id = 1, StudentId = 5, CourseSectionId = 1,
            AppliedElectivePoolId = 500, Status = RegistrationStatus.Active,
            Grade = GradeCode.BB, EctsSnapshot = null
        });
        await ctx.SaveChangesAsync();
        var poolOverrides = new Dictionary<int, int?> { [500] = 7 };
        var calc = new ElectivePoolProgressCalculator(ctx);

        var result = await calc.CalculateAsync(5, new[] { 500 }, poolOverrides);

        result[500].EarnedEcts.Should().Be(7, "poolEctsPerCourse should be used when EctsSnapshot is null");
    }

    [Fact]
    public async Task Calculate_UsesCourseEcts_WhenSnapshotAndPoolOverrideAreNull()
    {
        using var ctx = CreateContext();
        // EctsSnapshot = null, no pool override → Course.Ects = 6
        ctx.Courses.Add(new Course { Id = 1, Code = "C1", Name = "c", Ects = 6 });
        ctx.CourseSections.Add(new CourseSection { Id = 1, CourseId = 1, TermId = 1, Status = CourseSectionStatus.Active });
        ctx.CourseRegistrations.Add(new CourseRegistration
        {
            Id = 1, StudentId = 5, CourseSectionId = 1,
            AppliedElectivePoolId = 500, Status = RegistrationStatus.Active,
            Grade = GradeCode.BB, EctsSnapshot = null
        });
        await ctx.SaveChangesAsync();
        var calc = new ElectivePoolProgressCalculator(ctx);

        var result = await calc.CalculateAsync(5, new[] { 500 }, EmptyOverrides);

        result[500].EarnedEcts.Should().Be(6, "Course.Ects is the final fallback");
    }

    // ─── Multiple pools in a single call ─────────────────────────────────────────

    [Fact]
    public async Task Calculate_HandlesMultiplePools_Independently()
    {
        using var ctx = CreateContext();
        AddReg(ctx, 1, 5, 600, 1, RegistrationStatus.Active, GradeCode.AA, 6);
        AddReg(ctx, 2, 5, 600, 2, RegistrationStatus.Active, GradeCode.BA, 5);
        AddReg(ctx, 3, 5, 700, 3, RegistrationStatus.Active, GradeCode.CB, 4);
        AddReg(ctx, 4, 5, 700, 4, RegistrationStatus.Active, GradeCode.FF, 4); // fails — excluded
        await ctx.SaveChangesAsync();
        var calc = new ElectivePoolProgressCalculator(ctx);

        var result = await calc.CalculateAsync(5, new[] { 600, 700 }, EmptyOverrides);

        result[600].CompletedCourseCount.Should().Be(2);
        result[600].EarnedEcts.Should().Be(11);
        result[700].CompletedCourseCount.Should().Be(1);
        result[700].EarnedEcts.Should().Be(4);
    }

    // ─── Student isolation ────────────────────────────────────────────────────────

    [Fact]
    public async Task Calculate_ExcludesOtherStudentsRegs()
    {
        using var ctx = CreateContext();
        // Student 5 passes a course
        AddReg(ctx, 1, 5, 800, 1, RegistrationStatus.Active, GradeCode.BB, 6);
        // Student 99 also passes in the same pool — must NOT be counted for student 5
        AddReg(ctx, 2, 99, 800, 2, RegistrationStatus.Active, GradeCode.AA, 6);
        await ctx.SaveChangesAsync();
        var calc = new ElectivePoolProgressCalculator(ctx);

        var result = await calc.CalculateAsync(5, new[] { 800 }, EmptyOverrides);

        result[800].CompletedCourseCount.Should().Be(1, "Only the target student's regs must be counted");
        result[800].EarnedEcts.Should().Be(6);
    }

    // ─── Grade-visibility gate (tek kaynak: IGradeVisibilityService) ─────────────
    // Kanonik kural: geçmiş dönem her zaman görünür; yalnızca AKTİF dönemin ilan
    // edilmemiş notu gizlenir. Transkript / müfredat-durum / pool-progress aynı kural.

    private static void AddTerm(AppDbContext ctx, int id, bool isActivated, int year)
    {
        if (!ctx.AcademicTerms.Any(t => t.Id == id))
            ctx.AcademicTerms.Add(new AcademicTerm
            {
                Id = id,
                Name = $"T{id}",
                Year = year,
                IsActivated = isActivated,
                TermStart = new DateTime(year, 9, 1),
            });
    }

    [Fact]
    public async Task Calculate_HidesActiveTermGrade_WhenNotPublished_ButCountsPastTerm()
    {
        using var ctx = CreateContext();
        // Term 10 = AKTİF dönem, ilan KAPALI → gizli. Term 11 = geçmiş → her zaman görünür.
        AddTerm(ctx, 10, isActivated: true, year: 2025);
        AddTerm(ctx, 11, isActivated: false, year: 2024);
        AddReg(ctx, 1, 5, 900, 1, RegistrationStatus.Active, GradeCode.BB, 6, termId: 10);
        AddReg(ctx, 2, 5, 900, 2, RegistrationStatus.Active, GradeCode.AA, 5, termId: 11);
        await ctx.SaveChangesAsync();

        // Hiçbir dönem ilanı açık değil; ama 11 geçmiş dönem olduğu için yine görünür.
        var fakeWebModule = new FakeWebModuleControlService(visibleTermIds: new HashSet<int>());
        var calc = new ElectivePoolProgressCalculator(ctx, new GradeVisibilityService(ctx, fakeWebModule));

        var result = await calc.CalculateAsync(5, new[] { 900 }, EmptyOverrides);

        result[900].CompletedCourseCount.Should().Be(1, "Aktif dönemin ilan edilmemiş notu gizli; geçmiş dönem görünür");
        result[900].EarnedEcts.Should().Be(5);
    }

    [Fact]
    public async Task Calculate_CountsPastTermGrade_EvenWhenPublicationClosed()
    {
        // KANONİK KURAL + çift-sayım düzeltmesinin kanıtı: geçmiş dönemin notu, ilan modülü
        // kapalı olsa bile transkriptle aynı şekilde görünür sayılır. Eski resolver bunu
        // gizleyip dersi "alınmadı" gösteriyor ve senaryoda çift saydırıyordu.
        using var ctx = CreateContext();
        AddTerm(ctx, 30, isActivated: true, year: 2025);  // aktif dönem (başka bir dönem)
        AddTerm(ctx, 31, isActivated: false, year: 2024); // geçmiş dönem — dersin alındığı dönem
        AddReg(ctx, 1, 5, 990, 1, RegistrationStatus.Active, GradeCode.BB, 6, termId: 31);
        await ctx.SaveChangesAsync();

        var fakeWebModule = new FakeWebModuleControlService(visibleTermIds: new HashSet<int>());
        var calc = new ElectivePoolProgressCalculator(ctx, new GradeVisibilityService(ctx, fakeWebModule));

        var result = await calc.CalculateAsync(5, new[] { 990 }, EmptyOverrides);

        result[990].CompletedCourseCount.Should().Be(1, "Geçmiş dönem notu ilan modülünden bağımsız görünür");
        result[990].EarnedEcts.Should().Be(6);
    }

    [Fact]
    public async Task Calculate_DefaultsToVisible_WhenNoActiveTermExists()
    {
        // Aktif dönem yoksa hiçbir not gizlenmez (NullWebModuleControlService fallback).
        using var ctx = CreateContext();
        AddReg(ctx, 1, 5, 950, 1, RegistrationStatus.Active, GradeCode.CC, 6, termId: 999);
        await ctx.SaveChangesAsync();
        var calc = new ElectivePoolProgressCalculator(ctx); // resolver yok → GradeVisibilityService default

        var result = await calc.CalculateAsync(5, new[] { 950 }, EmptyOverrides);

        result[950].CompletedCourseCount.Should().Be(1, "Aktif dönem yoksa tüm dönemler görünür");
    }

    // ─── Makeup OR branch: aktif dönem letter kapalı ama makeup açık → not görünür ───

    [Fact]
    public async Task Calculate_CountsActiveTermGrade_WhenLetterClosedButMakeupOpen()
    {
        using var ctx = CreateContext();
        AddTerm(ctx, 20, isActivated: true, year: 2025); // aktif dönem
        AddReg(ctx, 1, 5, 980, 1, RegistrationStatus.Active, GradeCode.BB, 6, termId: 20);
        await ctx.SaveChangesAsync();

        var fakeWebModule = new FakeWebModuleControlService(
            letterGradeVisibleTermIds: new HashSet<int>(),
            makeupVisibleTermIds: new HashSet<int> { 20 });
        var calc = new ElectivePoolProgressCalculator(ctx, new GradeVisibilityService(ctx, fakeWebModule));

        var result = await calc.CalculateAsync(5, new[] { 980 }, EmptyOverrides);

        result[980].CompletedCourseCount.Should().Be(1,
            "Aktif dönemde makeup-letter-grade-publication açık → not görünür");
        result[980].EarnedEcts.Should().Be(6);
    }
}

/// <summary>
/// Test double for IWebModuleControlService: supports per-code, per-term visibility responses.
/// Use the two-parameter ctor to control letter-grade and makeup visibility independently.
/// Single-parameter ctor keeps backward compatibility for existing tests.
/// </summary>
file sealed class FakeWebModuleControlService : IWebModuleControlService
{
    private readonly HashSet<int> _letterGradeVisibleTermIds;
    private readonly HashSet<int> _makeupVisibleTermIds;

    /// <summary>Only terms in <paramref name="visibleTermIds"/> have letter-grade publication open.</summary>
    public FakeWebModuleControlService(HashSet<int> visibleTermIds)
        : this(visibleTermIds, new HashSet<int>()) { }

    /// <summary>Control letter-grade and makeup publication independently per term.</summary>
    public FakeWebModuleControlService(
        HashSet<int> letterGradeVisibleTermIds,
        HashSet<int> makeupVisibleTermIds)
    {
        _letterGradeVisibleTermIds = letterGradeVisibleTermIds;
        _makeupVisibleTermIds = makeupVisibleTermIds;
    }

    public WebModuleControlDefinition? GetDefinition(string code) => null;

    public Task<IReadOnlyList<WebModuleControlSummary>> GetSummariesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WebModuleControlSummary>>(Array.Empty<WebModuleControlSummary>());

    public Task<WebModuleAccessResult> EvaluateAsync(string code, int? termId = null, CancellationToken ct = default)
    {
        var isOpen = code switch
        {
            "letter-grade-publication" => termId.HasValue && _letterGradeVisibleTermIds.Contains(termId.Value),
            "makeup-letter-grade-publication" => termId.HasValue && _makeupVisibleTermIds.Contains(termId.Value),
            _ => false
        };
        return Task.FromResult(new WebModuleAccessResult(
            IsEnabled: true,
            IsOpen: isOpen,
            SelectedMode: isOpen ? WebModuleControlModes.Open : WebModuleControlModes.Closed,
            SelectedModeLabel: isOpen ? "Açık" : "Kapalı",
            EffectiveStateLabel: isOpen ? "Açık" : "Kapalı",
            Message: null));
    }
}
