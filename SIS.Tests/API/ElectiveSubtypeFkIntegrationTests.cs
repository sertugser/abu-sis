using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Controllers;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Tests.TestHelpers;

namespace SIS.Tests.API;

/// <summary>
/// Faz E — DB-driven <see cref="ElectiveSubtypeEntity"/> consumer testleri.
/// (Faz B B3b paralel destek testleri Faz E'de obsolete oldu — DTO field'ı
/// ve Create.ElectiveSubtype enum parametresi kalktığı için silindi.)
/// Doğrulanan davranışlar:
///   1) Default subtype'larla Create normal akışta çalışır
///   2) DTO sadece electiveSubtypeId döner (paralel enum field yok)
///   3) Admin'in eklediği custom subtype flag'lere göre dinamik validate edilir
///   4) Pasif (IsActive=false) subtype reddedilir
///   5) Bilinmeyen FK reddedilir
/// </summary>
public sealed class ElectiveSubtypeFkIntegrationTests : IDisposable
{
    private const int FacultyId = 1;
    private const int DepartmentId = 10;
    private const int ProgramId = 1000;
    private const int CurriculumId = 5000;

    private readonly AppDbContext _context;

    public ElectiveSubtypeFkIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        SeedBaseData();
    }

    private void SeedBaseData()
    {
        _context.Faculties.Add(new Faculty { Id = FacultyId, Name = "Mühendislik", Code = "ENG" });
        _context.Departments.Add(new Department { Id = DepartmentId, Name = "Bilgisayar Müh.", Code = "BLG", FacultyId = FacultyId });
        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId, Name = "Bilgisayar Mühendisliği", Code = "BLG-BSC",
            DepartmentId = DepartmentId, DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester, MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240
        });
        _context.Curricula.Add(new Curriculum
        {
            Id = CurriculumId, ProgramId = ProgramId, Version = "2024",
            IsActive = true, Status = CurriculumStatus.Draft, CreatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();
        _context.SeedElectiveSubtypeDefaults();
    }

    private ElectiveGroupController CreateController() => new(_context);

    private static string MessageOf(IActionResult result)
    {
        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        return bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value)!.ToString()!;
    }

    // ── 1) Default subtype Create normal çalışır ──────────────────────

    [Fact]
    public async Task Create_with_default_subtypeId_succeeds()
    {
        var controller = CreateController();
        var req = new ElectiveGroupController.CreateElectiveGroupRequest(
            Name: "OC default",
            Code: "OC-DEF",
            ElectiveSubtypeId: TestSeedExtensions.UniversityElectiveId);

        var result = await controller.Create(req, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ElectiveGroupController.ElectiveGroupListDto>().Subject;
        dto.ElectiveSubtypeId.Should().Be(TestSeedExtensions.UniversityElectiveId);
    }

    // ── 2) List DTO sadece electiveSubtypeId döner ────────────────────

    [Fact]
    public async Task List_dto_returns_electiveSubtypeId_only()
    {
        // Arrange — manuel olarak iki grup ekle (farklı tip).
        _context.ElectiveGroups.AddRange(
            new ElectiveGroup
            {
                Name = "TE-A", Code = "TE-A",
                ElectiveSubtypeId = TestSeedExtensions.TechnicalElectiveId,
                FacultyId = FacultyId, IsActive = true, CreatedAt = DateTime.UtcNow,
            },
            new ElectiveGroup
            {
                Name = "OC-A", Code = "OC-A",
                ElectiveSubtypeId = TestSeedExtensions.UniversityElectiveId,
                IsActive = true, CreatedAt = DateTime.UtcNow,
            });
        await _context.SaveChangesAsync();

        var controller = CreateController();

        var result = await controller.List(subtypeId: null, facultyId: null, departmentId: null, isActive: null, ct: CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<ElectiveGroupController.ElectiveGroupListDto>>().Subject.ToList();
        list.Should().HaveCount(2);
        list.Should().AllSatisfy(dto => dto.ElectiveSubtypeId.Should().BeGreaterThan(0));
    }

    // ── 3) Custom subtype: dinamik flag-based validation ──────────────

    [Fact]
    public async Task Create_with_custom_subtype_having_all_flags_false_succeeds_without_faculty_or_department()
    {
        var social = new ElectiveSubtypeEntity
        {
            Id = 100,
            Code = "SocialElective", Name = "Sosyal Seçmeli",
            RequiresFaculty = false, ForbidsFaculty = false,
            RequiresDepartment = false, ForbidsDepartment = false,
            IsActive = true, SortOrder = 10, CreatedAt = DateTime.UtcNow,
        };
        _context.ElectiveSubtypes.Add(social);
        await _context.SaveChangesAsync();

        var controller = CreateController();
        var req = new ElectiveGroupController.CreateElectiveGroupRequest(
            Name: "Sosyal Seçmeli I",
            Code: "SE-1",
            ElectiveSubtypeId: social.Id);

        var result = await controller.Create(req, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ElectiveGroupController.ElectiveGroupListDto>().Subject;
        dto.ElectiveSubtypeId.Should().Be(social.Id);
        dto.FacultyId.Should().BeNull();
        dto.DepartmentId.Should().BeNull();
    }

    [Fact]
    public async Task Create_with_custom_subtype_requiring_faculty_rejects_when_faculty_missing()
    {
        var prof = new ElectiveSubtypeEntity
        {
            Id = 101,
            Code = "ProfessionalElective", Name = "Mesleki Seçmeli",
            RequiresFaculty = true, ForbidsFaculty = false,
            RequiresDepartment = false, ForbidsDepartment = false,
            IsActive = true, SortOrder = 11, CreatedAt = DateTime.UtcNow,
        };
        _context.ElectiveSubtypes.Add(prof);
        await _context.SaveChangesAsync();

        var controller = CreateController();
        var req = new ElectiveGroupController.CreateElectiveGroupRequest(
            Name: "Mesleki I",
            Code: "PRF-1",
            ElectiveSubtypeId: prof.Id,
            FacultyId: null);

        var result = await controller.Create(req, CancellationToken.None);

        MessageOf(result).Should().Contain("Mesleki Seçmeli").And.Contain("fakülte zorunludur");
    }

    // ── 4) Pasif subtype reddedilir ───────────────────────────────────

    [Fact]
    public async Task Create_with_inactive_subtype_is_rejected()
    {
        var inactive = new ElectiveSubtypeEntity
        {
            Id = 102,
            Code = "RetiredElective", Name = "Emekli Tip",
            IsActive = false, SortOrder = 99, CreatedAt = DateTime.UtcNow,
        };
        _context.ElectiveSubtypes.Add(inactive);
        await _context.SaveChangesAsync();

        var controller = CreateController();
        var req = new ElectiveGroupController.CreateElectiveGroupRequest(
            Name: "Will fail",
            Code: "WF-1",
            ElectiveSubtypeId: inactive.Id);

        var result = await controller.Create(req, CancellationToken.None);

        MessageOf(result).Should().Contain("pasif");
    }

    // ── 5) Bilinmeyen FK reddedilir ───────────────────────────────────

    [Fact]
    public async Task Create_with_unknown_subtype_id_returns_bad_request()
    {
        var controller = CreateController();
        var req = new ElectiveGroupController.CreateElectiveGroupRequest(
            Name: "Unknown FK",
            Code: "UNK-1",
            ElectiveSubtypeId: 9999);

        var result = await controller.Create(req, CancellationToken.None);

        MessageOf(result).Should().Contain("9999").And.Contain("bulunamadı");
    }

    public void Dispose() => _context.Dispose();
}
