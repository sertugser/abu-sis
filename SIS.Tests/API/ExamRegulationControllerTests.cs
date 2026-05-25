using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Controllers;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.API;

public class ExamRegulationControllerTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ExamRegulationController _controller;

    public ExamRegulationControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        SeedBaseData();
        _controller = new ExamRegulationController(_context);
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
        var req = new SaveExamRegulationRequest(
            ProgramId: 1, DegreeLevel: DegreeLevel.Bachelor, TermType: TermType.Fall, Year: 2024,
            IsActive: true, Name: "Test Yönerge", MidtermCount: 2, FinalCount: 1,
            MinMidtermWeightPercent: 40, MaxMidtermWeightPercent: 60,
            MinFinalWeightPercent: 40, MaxFinalWeightPercent: 60,
            PassingGrade: 50, RawScoreFloor1: 0, RawScoreFloor2: 0, FinalExamFloor: 0);

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
            PassingGrade: 50, RawScoreFloor1: 0, RawScoreFloor2: 0, FinalExamFloor: 0);

        var result = await _controller.Create(req, default);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_Rejects_DuplicateScope()
    {
        var req = new SaveExamRegulationRequest(
            ProgramId: 1, DegreeLevel: DegreeLevel.Bachelor, TermType: TermType.Fall, Year: 2024,
            IsActive: true, Name: "İlk", MidtermCount: 1, FinalCount: 1,
            MinMidtermWeightPercent: 40, MaxMidtermWeightPercent: 60,
            MinFinalWeightPercent: 40, MaxFinalWeightPercent: 60,
            PassingGrade: 50, RawScoreFloor1: 0, RawScoreFloor2: 0, FinalExamFloor: 0);
        await _controller.Create(req, default);

        var dup = req with { Name = "Kopya" };
        var result = await _controller.Create(dup, default);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    public void Dispose() => _context.Dispose();
}
