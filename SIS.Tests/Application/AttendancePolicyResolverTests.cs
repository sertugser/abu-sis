using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public sealed class AttendancePolicyResolverTests : IDisposable
{
    private const int TermId = 1;
    private const int ProgramId = 5;
    private const int StudentId = 100;

    private readonly AppDbContext _context;
    private readonly AttendancePolicyResolver _resolver;

    public AttendancePolicyResolverTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);

        var config = new Mock<ISystemConfigProvider>();
        config.Setup(c => c.GetIntAsync("Attendance.TermWeekCount", 14, It.IsAny<CancellationToken>())).ReturnsAsync(14);
        config.Setup(c => c.GetDecimalAsync("Attendance.TheoryAbsenceLimitPercent", 30m, It.IsAny<CancellationToken>())).ReturnsAsync(30m);
        config.Setup(c => c.GetDecimalAsync("Attendance.LabAbsenceLimitPercent", 20m, It.IsAny<CancellationToken>())).ReturnsAsync(20m);
        config.Setup(c => c.GetStringAsync("Attendance.Method", "Separate", It.IsAny<CancellationToken>())).ReturnsAsync("Separate");

        _resolver = new AttendancePolicyResolver(_context, config.Object);
    }

    [Fact]
    public async Task ResolveAsync_NoTermPolicy_ReturnsGlobalDefault()
    {
        var policy = await _resolver.ResolveAsync(TermId, programId: null);

        policy.TermWeekCount.Should().Be(14);
        policy.TheoryAbsenceLimitPercent.Should().Be(30m);
        policy.LabAbsenceLimitPercent.Should().Be(20m);
        policy.Method.Should().Be(AttendanceMethod.Separate);
    }

    [Fact]
    public async Task ResolveAsync_TermGeneralPolicy_OverridesGlobal()
    {
        SeedPolicy(programId: null, weeks: 16, theory: 40, lab: 25, method: "TotalHours");

        // Program 5 için override yok → dönem geneli kullanılır.
        var policy = await _resolver.ResolveAsync(TermId, programId: ProgramId);

        policy.TermWeekCount.Should().Be(16);
        policy.TheoryAbsenceLimitPercent.Should().Be(40m);
        policy.Method.Should().Be(AttendanceMethod.TotalHours);
    }

    [Fact]
    public async Task ResolveAsync_ProgramOverride_BeatsTermGeneral()
    {
        SeedPolicy(programId: null, weeks: 16, theory: 40, lab: 25, method: "TotalHours");
        SeedPolicy(programId: ProgramId, weeks: 12, theory: 50, lab: 10, method: "Passive");

        var policy = await _resolver.ResolveAsync(TermId, programId: ProgramId);

        policy.TermWeekCount.Should().Be(12);
        policy.TheoryAbsenceLimitPercent.Should().Be(50m);
        policy.Method.Should().Be(AttendanceMethod.Passive);
    }

    [Fact]
    public async Task ResolveForStudentsAsync_UsesStudentMainProgram()
    {
        SeedPolicy(programId: null, weeks: 16, theory: 40, lab: 25, method: "Separate");
        SeedPolicy(programId: ProgramId, weeks: 12, theory: 50, lab: 10, method: "Passive");
        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = StudentId,
            ProgramId = ProgramId,
            Type = StudentProgramType.Main,
            IsActive = true,
        });
        _context.SaveChanges();

        var result = await _resolver.ResolveForStudentsAsync(new[] { StudentId }, TermId);

        result.Should().ContainKey(StudentId);
        result[StudentId].TheoryAbsenceLimitPercent.Should().Be(50m, "öğrencinin programının override'ı uygulanır");
        result[StudentId].Method.Should().Be(AttendanceMethod.Passive);
    }

    [Fact]
    public async Task ResolveForStudentsAsync_NoProgram_FallsBackToTermGeneral()
    {
        SeedPolicy(programId: null, weeks: 16, theory: 40, lab: 25, method: "Separate");
        // Öğrencinin Main programı yok.

        var result = await _resolver.ResolveForStudentsAsync(new[] { StudentId }, TermId);

        result[StudentId].TheoryAbsenceLimitPercent.Should().Be(40m, "program yoksa dönem geneline düşer");
    }

    private void SeedPolicy(int? programId, int weeks, decimal theory, decimal lab, string method)
    {
        _context.TermAttendancePolicies.Add(new TermAttendancePolicy
        {
            TermId = TermId,
            ProgramId = programId,
            TermWeekCount = weeks,
            TheoryAbsenceLimitPercent = theory,
            LabAbsenceLimitPercent = lab,
            Method = method,
            CreatedAt = DateTime.UtcNow,
        });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();
}
