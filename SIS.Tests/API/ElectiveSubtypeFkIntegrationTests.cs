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
/// Faz B B3b — DB-driven <see cref="ElectiveSubtypeEntity"/> consumer testleri.
/// Doğrulanan davranışlar:
///   1) Mevcut enum-only request'ler bozulmadan çalışır (geri uyumluluk).
///   2) Yeni FK-only request'ler çalışır.
///   3) FK + enum birlikte gönderilirse FK öncelikli.
///   4) DTO yanıtları hem enum hem FK içerir (paralel destek).
///   5) Admin'in eklediği custom subtype flag'lere göre dinamik validate edilir.
///   6) Pasif (IsActive=false) subtype reddedilir.
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

    // ── 1) Geri uyumluluk: enum-only Create hâlâ çalışır ───────────────

    [Fact]
    public async Task Create_with_enum_only_resolves_subtype_via_DB_and_succeeds()
    {
        // Arrange — sadece enum (eski frontend davranışı), FK alanı yok.
        var controller = CreateController();
        var req = new ElectiveGroupController.CreateElectiveGroupRequest(
            Name: "Teknik Seçmeli BLG",
            Code: "TE-BLG-OLD",
            ElectiveSubtype: ElectiveSubtype.TechnicalElective,
            ElectiveSubtypeId: null,
            FacultyId: FacultyId);

        // Act
        var result = await controller.Create(req, CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ElectiveGroupController.ElectiveGroupListDto>().Subject;
        dto.ElectiveSubtype.Should().Be(ElectiveSubtype.TechnicalElective);
        dto.ElectiveSubtypeId.Should().Be((int)ElectiveSubtype.TechnicalElective);
    }

    // ── 2) Yeni FK-only Create çalışır + enum'a doğru map'lenir ────────

    [Fact]
    public async Task Create_with_FK_only_succeeds_and_maps_to_enum()
    {
        // Arrange — sadece FK gönder, enum default(0). Backend Code'dan enum'a map etmeli.
        var controller = CreateController();
        var req = new ElectiveGroupController.CreateElectiveGroupRequest(
            Name: "OC FK-only",
            Code: "OC-FK-ONLY",
            ElectiveSubtype: default,
            ElectiveSubtypeId: (int)ElectiveSubtype.UniversityElective);

        // Act
        var result = await controller.Create(req, CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ElectiveGroupController.ElectiveGroupListDto>().Subject;
        dto.ElectiveSubtypeId.Should().Be((int)ElectiveSubtype.UniversityElective);
        dto.ElectiveSubtype.Should().Be(ElectiveSubtype.UniversityElective);
    }

    // ── 3) FK > enum precedence ───────────────────────────────────────

    [Fact]
    public async Task Create_with_both_FK_and_enum_uses_FK_when_they_differ()
    {
        // Arrange — enum=TE (faculty zorunlu) ama FK=NDE (faculty opsiyonel).
        // FK kazanırsa NDE flag'leri uygulanır → faculty olmadan da geçmeli.
        var controller = CreateController();
        var req = new ElectiveGroupController.CreateElectiveGroupRequest(
            Name: "FK precedence test",
            Code: "FK-WINS",
            ElectiveSubtype: ElectiveSubtype.TechnicalElective,
            ElectiveSubtypeId: (int)ElectiveSubtype.NonDepartmentElective,
            FacultyId: null);

        // Act
        var result = await controller.Create(req, CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ElectiveGroupController.ElectiveGroupListDto>().Subject;
        dto.ElectiveSubtypeId.Should().Be((int)ElectiveSubtype.NonDepartmentElective);
        dto.ElectiveSubtype.Should().Be(ElectiveSubtype.NonDepartmentElective);
    }

    // ── 4) DTO paralel destek ─────────────────────────────────────────

    [Fact]
    public async Task List_dto_returns_both_enum_and_FK_for_existing_groups()
    {
        // Arrange — manuel olarak iki grup ekle (FK set edilmiş).
        _context.ElectiveGroups.AddRange(
            new ElectiveGroup
            {
                Name = "TE-A", Code = "TE-A",
                ElectiveSubtype = ElectiveSubtype.TechnicalElective,
                ElectiveSubtypeId = (int)ElectiveSubtype.TechnicalElective,
                FacultyId = FacultyId, IsActive = true, CreatedAt = DateTime.UtcNow,
            },
            new ElectiveGroup
            {
                Name = "OC-A", Code = "OC-A",
                ElectiveSubtype = ElectiveSubtype.UniversityElective,
                ElectiveSubtypeId = (int)ElectiveSubtype.UniversityElective,
                IsActive = true, CreatedAt = DateTime.UtcNow,
            });
        await _context.SaveChangesAsync();

        var controller = CreateController();

        // Act
        var result = await controller.List(subtype: null, facultyId: null, departmentId: null, isActive: null, CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<ElectiveGroupController.ElectiveGroupListDto>>().Subject.ToList();
        list.Should().HaveCount(2);
        list.Should().AllSatisfy(dto =>
        {
            dto.ElectiveSubtypeId.Should().Be((int)dto.ElectiveSubtype);
        });
    }

    // ── 5) Custom subtype: dinamik flag-based validation ──────────────

    [Fact]
    public async Task Create_with_custom_subtype_having_all_flags_false_succeeds_without_faculty_or_department()
    {
        // Arrange — admin yeni "Sosyal Seçmeli" tipi ekledi (tüm scope flag'leri false).
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
            ElectiveSubtype: default,
            ElectiveSubtypeId: social.Id);

        // Act — faculty/dept yok, custom tip izin verir.
        var result = await controller.Create(req, CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ElectiveGroupController.ElectiveGroupListDto>().Subject;
        dto.ElectiveSubtypeId.Should().Be(social.Id);
        dto.FacultyId.Should().BeNull();
        dto.DepartmentId.Should().BeNull();
    }

    [Fact]
    public async Task Create_with_custom_subtype_requiring_faculty_rejects_when_faculty_missing()
    {
        // Arrange — admin "Mesleki Seçmeli" ekledi (RequiresFaculty=true) ve faculty atlandı.
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
            ElectiveSubtype: default,
            ElectiveSubtypeId: prof.Id,
            FacultyId: null);

        // Act
        var result = await controller.Create(req, CancellationToken.None);

        // Assert — dinamik mesaj custom Name'i içermeli.
        MessageOf(result).Should().Contain("Mesleki Seçmeli").And.Contain("fakülte zorunludur");
    }

    // ── 6) Pasif subtype reddedilir ───────────────────────────────────

    [Fact]
    public async Task Create_with_inactive_subtype_is_rejected()
    {
        // Arrange — admin pasif yapmış.
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
            ElectiveSubtype: default,
            ElectiveSubtypeId: inactive.Id);

        // Act
        var result = await controller.Create(req, CancellationToken.None);

        // Assert
        MessageOf(result).Should().Contain("pasif");
    }

    // ── 7) Bilinmeyen FK reddedilir ───────────────────────────────────

    [Fact]
    public async Task Create_with_unknown_subtype_id_returns_bad_request()
    {
        // Arrange — DB'de Id=9999 yok.
        var controller = CreateController();
        var req = new ElectiveGroupController.CreateElectiveGroupRequest(
            Name: "Unknown FK",
            Code: "UNK-1",
            ElectiveSubtype: default,
            ElectiveSubtypeId: 9999);

        // Act
        var result = await controller.Create(req, CancellationToken.None);

        // Assert
        MessageOf(result).Should().Contain("9999").And.Contain("bulunamadı");
    }

    public void Dispose() => _context.Dispose();
}
