using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.API.Controllers;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using System.Security.Claims;

namespace SIS.Tests.API;

public class ExamRegulationControllerTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<ISystemConfigProvider> _configMock;
    private readonly ExamRegulationController _controller;

    public ExamRegulationControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        SeedBaseData();

        _configMock = new Mock<ISystemConfigProvider>();
        // Default: lock is off — existing tests are unaffected
        _configMock
            .Setup(c => c.GetBoolAsync("ExamRegulation.GlobalEditLock", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _controller = CreateController(_configMock, isSystemAdmin: false);
    }

    private ExamRegulationController CreateController(Mock<ISystemConfigProvider> configMock, bool isSystemAdmin)
    {
        var controller = new ExamRegulationController(_context, configMock.Object);
        var claims = new List<Claim>();
        if (isSystemAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "SystemAdministrator"));
        var identity = new ClaimsIdentity(claims, "Test");
        var user = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        return controller;
    }

    private void SeedBaseData()
    {
        _context.Faculties.Add(new Faculty { Id = 1, Name = "Test Faculty", Code = "TF" });
        _context.Departments.Add(new Department { Id = 1, Name = "Test Dept", Code = "TD", FacultyId = 1 });
        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = 1, Name = "Test Program", Code = "TP", DepartmentId = 1,
            DegreeLevel = DegreeLevel.Bachelor, EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 8, TotalRequiredEcts = 240
        });
        _context.SaveChanges();
    }

    private static SaveExamRegulationRequest DefaultRequest(int year = 2024, string name = "Test Yönerge") =>
        new(
            ProgramId: 1, DegreeLevel: DegreeLevel.Bachelor, TermType: TermType.Fall, Year: year,
            IsActive: true, Name: name, MidtermCount: 2, FinalCount: 1,
            MinMidtermWeightPercent: 40, MaxMidtermWeightPercent: 60,
            MinFinalWeightPercent: 40, MaxFinalWeightPercent: 60,
            PassingGrade: 50, RawScoreFloor1: 0, RawScoreFloor2: 0, FinalExamFloor: 0,
            AAMin: 91, BAMin: 81, BBMin: 71, CBMin: 61, CCMin: 55, DCMin: 50, DDMin: 45,
            LetterRangesEditable: true);

    [Fact]
    public async Task List_ReturnsEmpty_WhenNoRegulations()
    {
        var result = await _controller.List(null, default);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var items = ok.Value.Should().BeAssignableTo<IEnumerable<ExamRegulationDto>>().Subject;
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_PersistsRegulation_AndListReturnsIt()
    {
        var req = DefaultRequest();

        var created = await _controller.Create(req, default);
        created.Should().BeOfType<OkObjectResult>();

        var listResult = await _controller.List(null, default);
        var ok = listResult.Should().BeOfType<OkObjectResult>().Subject;
        var items = ok.Value.Should().BeAssignableTo<IEnumerable<ExamRegulationDto>>().Subject;
        items.Should().HaveCount(1);
        items.First().Name.Should().Be("Test Yönerge");
    }

    [Fact]
    public async Task Create_Rejects_WhenMinWeightExceedsMax()
    {
        var req = new SaveExamRegulationRequest(
            ProgramId: 1, DegreeLevel: DegreeLevel.Bachelor, TermType: TermType.Fall, Year: 2025,
            IsActive: true, Name: "Hatalı", MidtermCount: 1, FinalCount: 1,
            MinMidtermWeightPercent: 70, MaxMidtermWeightPercent: 60,
            MinFinalWeightPercent: 40, MaxFinalWeightPercent: 60,
            PassingGrade: 50, RawScoreFloor1: 0, RawScoreFloor2: 0, FinalExamFloor: 0,
            AAMin: 91, BAMin: 81, BBMin: 71, CBMin: 61, CCMin: 55, DCMin: 50, DDMin: 45,
            LetterRangesEditable: true);

        var result = await _controller.Create(req, default);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_Rejects_DuplicateScope()
    {
        var req = DefaultRequest(year: 2024, name: "İlk");
        await _controller.Create(req, default);

        var dup = req with { Name = "Kopya" };
        var result = await _controller.Create(dup, default);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Clone_Rejects_InvalidYear()
    {
        var req = DefaultRequest(year: 2024, name: "Kaynak");
        var created = await _controller.Create(req, default);
        var createdId = ((ExamRegulationDto)((OkObjectResult)created).Value!).Id;

        var result = await _controller.Clone(createdId, new CloneExamRegulationRequest(null, 1500), default);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_Rejects_WhenLetterRangesNotDescending()
    {
        // AAMin <= BAMin — violates descending order rule
        var req = new SaveExamRegulationRequest(
            ProgramId: 1, DegreeLevel: DegreeLevel.Bachelor, TermType: TermType.Fall, Year: 2026,
            IsActive: true, Name: "Hatalı Aralık", MidtermCount: 1, FinalCount: 1,
            MinMidtermWeightPercent: 40, MaxMidtermWeightPercent: 60,
            MinFinalWeightPercent: 40, MaxFinalWeightPercent: 60,
            PassingGrade: 50, RawScoreFloor1: 0, RawScoreFloor2: 0, FinalExamFloor: 0,
            AAMin: 70, BAMin: 81, BBMin: 71, CBMin: 61, CCMin: 55, DCMin: 50, DDMin: 45,
            LetterRangesEditable: true);

        var result = await _controller.Create(req, default);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_Forbidden_WhenGloballyLocked_AndNotSysAdmin()
    {
        var lockedConfigMock = new Mock<ISystemConfigProvider>();
        lockedConfigMock
            .Setup(c => c.GetBoolAsync("ExamRegulation.GlobalEditLock", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = CreateController(lockedConfigMock, isSystemAdmin: false);

        var result = await controller.Create(DefaultRequest(), default);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Create_Allowed_WhenGloballyLocked_ButSysAdmin()
    {
        var lockedConfigMock = new Mock<ISystemConfigProvider>();
        lockedConfigMock
            .Setup(c => c.GetBoolAsync("ExamRegulation.GlobalEditLock", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = CreateController(lockedConfigMock, isSystemAdmin: true);

        var result = await controller.Create(DefaultRequest(), default);

        // Should NOT be a 403 — proceeds to normal result (OkObjectResult or BadRequest from validation)
        result.Should().NotBeOfType<ObjectResult>()
            .And.Subject.Should().Match(r => r is OkObjectResult || r is BadRequestObjectResult);
    }

    [Fact]
    public async Task Create_PersistsRelativeFields_AndGetReturnsThem()
    {
        var req = new SaveExamRegulationRequest(
            ProgramId: 1, DegreeLevel: DegreeLevel.Bachelor, TermType: TermType.Fall, Year: 2027,
            IsActive: true, Name: "Bağıl Yönerge", MidtermCount: 2, FinalCount: 1,
            MinMidtermWeightPercent: 40, MaxMidtermWeightPercent: 60,
            MinFinalWeightPercent: 40, MaxFinalWeightPercent: 60,
            PassingGrade: 50, RawScoreFloor1: 0, RawScoreFloor2: 0, FinalExamFloor: 0,
            AAMin: 91, BAMin: 81, BBMin: 71, CBMin: 61, CCMin: 55, DCMin: 50, DDMin: 45,
            LetterRangesEditable: true,
            EvaluationType: GradeEvaluationType.Relative,
            RelativeMinStudentCount: 15,
            RelativeInclusionLimit: 30m,
            RelativeAbsoluteFloor: 25m,
            TAAMin: 72, TBAMin: 64, TBBMin: 58, TCBMin: 51, TCCMin: 46, TDCMin: 41, TDDMin: 36);

        var created = await _controller.Create(req, default);
        var createdDto = ((ExamRegulationDto)((OkObjectResult)created).Value!);

        var getResult = await _controller.Get(createdDto.Id, default);
        var ok = getResult.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ExamRegulationDto>().Subject;

        dto.EvaluationType.Should().Be("Relative");
        dto.RelativeMinStudentCount.Should().Be(15);
        dto.RelativeInclusionLimit.Should().Be(30m);
        dto.RelativeAbsoluteFloor.Should().Be(25m);
        dto.TAAMin.Should().Be(72);
        dto.TBAMin.Should().Be(64);
        dto.TBBMin.Should().Be(58);
        dto.TCBMin.Should().Be(51);
        dto.TCCMin.Should().Be(46);
        dto.TDCMin.Should().Be(41);
        dto.TDDMin.Should().Be(36);
    }

    [Fact]
    public async Task Create_Rejects_WhenRelativeTThresholdsNotDescending()
    {
        var req = new SaveExamRegulationRequest(
            ProgramId: 1, DegreeLevel: DegreeLevel.Bachelor, TermType: TermType.Fall, Year: 2028,
            IsActive: true, Name: "Bozuk T-Skor", MidtermCount: 1, FinalCount: 1,
            MinMidtermWeightPercent: 40, MaxMidtermWeightPercent: 60,
            MinFinalWeightPercent: 40, MaxFinalWeightPercent: 60,
            PassingGrade: 50, RawScoreFloor1: 0, RawScoreFloor2: 0, FinalExamFloor: 0,
            AAMin: 91, BAMin: 81, BBMin: 71, CBMin: 61, CCMin: 55, DCMin: 50, DDMin: 45,
            LetterRangesEditable: true,
            EvaluationType: GradeEvaluationType.Relative,
            TAAMin: 30, TBAMin: 64, TBBMin: 58, TCBMin: 51, TCCMin: 46, TDCMin: 41, TDDMin: 70);

        var result = await _controller.Create(req, default);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Clone_PreservesRelativeFields()
    {
        var req = new SaveExamRegulationRequest(
            ProgramId: 1, DegreeLevel: DegreeLevel.Bachelor, TermType: TermType.Fall, Year: 2029,
            IsActive: true, Name: "Bağıl Kaynak", MidtermCount: 2, FinalCount: 1,
            MinMidtermWeightPercent: 40, MaxMidtermWeightPercent: 60,
            MinFinalWeightPercent: 40, MaxFinalWeightPercent: 60,
            PassingGrade: 50, RawScoreFloor1: 0, RawScoreFloor2: 0, FinalExamFloor: 0,
            AAMin: 91, BAMin: 81, BBMin: 71, CBMin: 61, CCMin: 55, DCMin: 50, DDMin: 45,
            LetterRangesEditable: true,
            EvaluationType: GradeEvaluationType.Relative,
            RelativeMinStudentCount: 20,
            TAAMin: 72, TBAMin: 64, TBBMin: 58, TCBMin: 51, TCCMin: 46, TDCMin: 41, TDDMin: 36);

        var created = await _controller.Create(req, default);
        var sourceId = ((ExamRegulationDto)((OkObjectResult)created).Value!).Id;

        var cloneResult = await _controller.Clone(sourceId, new CloneExamRegulationRequest(null, 2030), default);
        var ok = cloneResult.Should().BeOfType<OkObjectResult>().Subject;
        var cloneDto = ok.Value.Should().BeOfType<ExamRegulationDto>().Subject;

        cloneDto.EvaluationType.Should().Be("Relative");
        cloneDto.RelativeMinStudentCount.Should().Be(20);
        cloneDto.TAAMin.Should().Be(72);
        cloneDto.TBAMin.Should().Be(64);
        cloneDto.TBBMin.Should().Be(58);
        cloneDto.TCBMin.Should().Be(51);
        cloneDto.TCCMin.Should().Be(46);
        cloneDto.TDCMin.Should().Be(41);
        cloneDto.TDDMin.Should().Be(36);
    }

    public void Dispose() => _context.Dispose();
}
