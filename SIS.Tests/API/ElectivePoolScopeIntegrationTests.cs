using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Controllers;
using SIS.Application.ElectiveGroups.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.API;

/// <summary>
/// Faz A integration tests for elective-pool scope validation pipelines:
///   - <see cref="ElectiveGroupController.Create"/>
///   - <see cref="ElectiveGroupController.Update"/>
///   - <see cref="CurriculumController.CreatePool"/>
///
/// Tests directly invoke controller methods with EF in-memory provider; no
/// HTTP pipeline / auth simulation is needed because the validations under
/// test live in the action methods, not in the auth pipeline.
/// </summary>
public sealed class ElectivePoolScopeIntegrationTests : IDisposable
{
    private const int FacultyEngId = 1;
    private const int FacultyLawId = 2;
    private const int DeptCompEngId = 10;
    private const int DeptElecEngId = 20;
    private const int DeptLawDeptId = 99;
    private const int ProgramId = 1000;
    private const int CurriculumId = 5000;

    private readonly AppDbContext _context;

    public ElectivePoolScopeIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        SeedBaseData();
    }

    private void SeedBaseData()
    {
        _context.Faculties.AddRange(
            new Faculty { Id = FacultyEngId, Name = "Mühendislik", Code = "ENG" },
            new Faculty { Id = FacultyLawId, Name = "Hukuk", Code = "LAW" });

        _context.Departments.AddRange(
            new Department { Id = DeptCompEngId, Name = "Bilgisayar Müh.", Code = "BLG", FacultyId = FacultyEngId },
            new Department { Id = DeptElecEngId, Name = "Elektrik-Elektronik Müh.", Code = "EEM", FacultyId = FacultyEngId },
            new Department { Id = DeptLawDeptId, Name = "Hukuk Bölümü", Code = "HUK", FacultyId = FacultyLawId });

        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId,
            Name = "Bilgisayar Mühendisliği",
            Code = "BLG-BSC",
            DepartmentId = DeptCompEngId,
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240
        });

        _context.Curricula.Add(new Curriculum
        {
            Id = CurriculumId,
            ProgramId = ProgramId,
            Version = "2024",
            IsActive = true,
            Status = CurriculumStatus.Draft,
            CreatedAt = DateTime.UtcNow
        });

        _context.SaveChanges();
    }

    private ElectiveGroupController CreateGroupController() => new(_context);

    private CurriculumController CreateCurriculumController() =>
        new(_context, new ElectiveGroupScopeValidator());

    private static string MessageOf(IActionResult result)
    {
        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        return bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value)!.ToString()!;
    }

    // ── Group 1: ElectiveGroupController.Create ────────────────────────

    [Fact]
    public async Task Create_TechnicalElective_with_faculty_and_department_returns_ok_with_department_set()
    {
        // Arrange
        var controller = CreateGroupController();
        var req = new ElectiveGroupController.CreateElectiveGroupRequest(
            Name: "Teknik Seçmeli BLG",
            Code: "TE-BLG",
            ElectiveSubtype: ElectiveSubtype.TechnicalElective,
            FacultyId: FacultyEngId,
            DepartmentId: DeptCompEngId,
            Description: null);

        // Act
        var result = await controller.Create(req, CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ElectiveGroupController.ElectiveGroupListDto>().Subject;
        dto.FacultyId.Should().Be(FacultyEngId);
        dto.DepartmentId.Should().Be(DeptCompEngId);
        dto.ElectiveSubtype.Should().Be(ElectiveSubtype.TechnicalElective);
    }

    [Fact]
    public async Task Create_DepartmentId_without_FacultyId_returns_bad_request()
    {
        // Arrange — DeptId set, FacultyId null. NDE seçtik çünkü TE alt-tipi
        // önce "TE için fakülte zorunlu" guard'ını tetikler ve gerçek "Bölüm
        // seçildiğinde fakülte" yolunu maskeler.
        var controller = CreateGroupController();
        var req = new ElectiveGroupController.CreateElectiveGroupRequest(
            Name: "NDE bölüm var fakülte yok",
            Code: "NDE-NOFAC",
            ElectiveSubtype: ElectiveSubtype.NonDepartmentElective,
            FacultyId: null,
            DepartmentId: DeptCompEngId);

        // Act
        var result = await controller.Create(req, CancellationToken.None);

        // Assert
        MessageOf(result).Should().Contain("Bölüm seçildiğinde fakülte");
    }

    [Fact]
    public async Task Create_UniversityElective_with_DepartmentId_returns_bad_request()
    {
        // Arrange
        // Create flow'unda OC + DeptId üç kombinasyonun hepsi reddedilir:
        //   - FacultyId verilirse: line 127 ("Üniversite Seçmeli için fakülte belirtilmemelidir") yakalar.
        //   - FacultyId verilmezse: line 131 ("Bölüm seçildiğinde fakülte de zorunludur") yakalar.
        // Line 135-136'daki spesifik "Üniversite Seçmeli için bölüm" guard'ı Create'te
        // önceki guard'larca dolaylı olarak korunduğu için pratikte unreachable; bu testte
        // her iki yan yol da OC+DeptId kombinasyonunun BadRequest döndürdüğünü kanıtlar.
        var controller = CreateGroupController();

        var reqNoFaculty = new ElectiveGroupController.CreateElectiveGroupRequest(
            Name: "OC + Bölüm (no faculty)",
            Code: "OC-WITHDEPT-NF",
            ElectiveSubtype: ElectiveSubtype.UniversityElective,
            FacultyId: null,
            DepartmentId: DeptCompEngId);
        var reqWithFaculty = new ElectiveGroupController.CreateElectiveGroupRequest(
            Name: "OC + Bölüm (with faculty)",
            Code: "OC-WITHDEPT-WF",
            ElectiveSubtype: ElectiveSubtype.UniversityElective,
            FacultyId: FacultyEngId,
            DepartmentId: DeptCompEngId);

        // Act
        var result1 = await controller.Create(reqNoFaculty, CancellationToken.None);
        var result2 = await controller.Create(reqWithFaculty, CancellationToken.None);

        // Assert — her iki yol da BadRequest. Mesajlardan en az biri "bölüm" veya
        // "Üniversite Seçmeli" hakkında olmalı.
        var msg1 = MessageOf(result1);
        var msg2 = MessageOf(result2);

        msg1.Should().Contain("Bölüm seçildiğinde fakülte");
        // OC + Faculty guard'ı (line 127), OC + DeptId guard'ından (line 135) önce
        // bu kombinasyonu yakalar — bu da geçerli bir reddetme yoludur.
        msg2.Should().Contain("Üniversite Seçmeli");
    }

    [Fact]
    public async Task Create_department_in_different_faculty_returns_bad_request()
    {
        // Arrange — DeptLawDeptId (99) Hukuk fakültesine ait, ama FacultyEngId (1) gönderiyoruz.
        var controller = CreateGroupController();
        var req = new ElectiveGroupController.CreateElectiveGroupRequest(
            Name: "Yanlış fakülte-bölüm",
            Code: "TE-MISMATCH",
            ElectiveSubtype: ElectiveSubtype.TechnicalElective,
            FacultyId: FacultyEngId,
            DepartmentId: DeptLawDeptId);

        // Act
        var result = await controller.Create(req, CancellationToken.None);

        // Assert
        MessageOf(result).Should().Contain("Bölüm bulunamadı veya seçilen fakülteye ait değil");
    }

    // ── Group 2: ElectiveGroupController.Update scope-clear semantics ─

    [Fact]
    public async Task Update_ClearFaculty_and_ClearDepartment_makes_group_school_wide()
    {
        // Arrange — fakülte+bölüm scope'lu mevcut bir grup ekle.
        var group = new ElectiveGroup
        {
            Name = "Eski TE",
            Code = "TE-CLEAR",
            ElectiveSubtype = ElectiveSubtype.TechnicalElective,
            FacultyId = FacultyEngId,
            DepartmentId = DeptCompEngId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.ElectiveGroups.Add(group);
        await _context.SaveChangesAsync();

        var controller = CreateGroupController();
        var req = new ElectiveGroupController.UpdateElectiveGroupRequest(
            ClearFaculty: true,
            ClearDepartment: true);

        // Act
        var result = await controller.Update(group.Id, req, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var reloaded = await _context.ElectiveGroups.AsNoTracking().FirstAsync(eg => eg.Id == group.Id);
        reloaded.FacultyId.Should().BeNull();
        reloaded.DepartmentId.Should().BeNull();
    }

    [Fact]
    public async Task Update_ClearFaculty_without_ClearDepartment_when_department_set_returns_bad_request()
    {
        // Arrange — fakülte+bölüm scope'lu grup; sadece fakülte temizlemeye çalış.
        var group = new ElectiveGroup
        {
            Name = "TE bağımlı",
            Code = "TE-DEP-LEFTOVER",
            ElectiveSubtype = ElectiveSubtype.TechnicalElective,
            FacultyId = FacultyEngId,
            DepartmentId = DeptCompEngId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.ElectiveGroups.Add(group);
        await _context.SaveChangesAsync();

        var controller = CreateGroupController();
        var req = new ElectiveGroupController.UpdateElectiveGroupRequest(
            ClearFaculty: true,
            ClearDepartment: false);

        // Act
        var result = await controller.Update(group.Id, req, CancellationToken.None);

        // Assert
        MessageOf(result).Should().Contain("Fakülte temizlenirken bölüm de temizlenmelidir");
    }

    [Fact]
    public async Task Update_scope_change_with_existing_curriculum_bindings_returns_bad_request()
    {
        // Arrange — fakülte scope'lu grup + mevcut müfredat bağlantısı.
        var group = new ElectiveGroup
        {
            Name = "TE bağlı",
            Code = "TE-BOUND",
            ElectiveSubtype = ElectiveSubtype.TechnicalElective,
            FacultyId = FacultyEngId,
            DepartmentId = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.ElectiveGroups.Add(group);
        await _context.SaveChangesAsync();

        _context.CurriculumElectivePools.Add(new CurriculumElectivePool
        {
            CurriculumId = CurriculumId,
            ElectiveGroupId = group.Id,
            SuggestedSemesterStart = 1,
            SuggestedSemesterEnd = 8,
            RequiredCourseCount = 3,
            RequiredEcts = 9,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var controller = CreateGroupController();
        // Scope değişimi: faculty Eng → Law (mevcut bağlantılar engellemeli).
        var req = new ElectiveGroupController.UpdateElectiveGroupRequest(
            FacultyId: FacultyLawId);

        // Act
        var result = await controller.Update(group.Id, req, CancellationToken.None);

        // Assert
        MessageOf(result).Should().Contain("Bu grup mevcut müfredatlara bağlı");
    }

    // ── Group 3: CurriculumController.CreatePool scope ladder ─────────

    [Fact]
    public async Task CreatePool_when_group_faculty_matches_curriculum_faculty_returns_ok()
    {
        // Arrange — fakülte scope'lu (Eng) grup, müfredat da Eng/BLG'ye ait.
        var group = new ElectiveGroup
        {
            Name = "TE Eng",
            Code = "TE-ENG-POOL",
            ElectiveSubtype = ElectiveSubtype.TechnicalElective,
            FacultyId = FacultyEngId,
            DepartmentId = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.ElectiveGroups.Add(group);
        await _context.SaveChangesAsync();

        var controller = CreateCurriculumController();
        var req = new CurriculumController.CreatePoolRequest(
            ElectiveGroupId: group.Id,
            SuggestedSemesterStart: 5,
            SuggestedSemesterEnd: 8,
            RequiredCourseCount: 3,
            RequiredEcts: 9);

        // Act
        var result = await controller.CreatePool(CurriculumId, req, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var pool = await _context.CurriculumElectivePools.AsNoTracking()
            .FirstOrDefaultAsync(p => p.CurriculumId == CurriculumId && p.ElectiveGroupId == group.Id);
        pool.Should().NotBeNull();
        pool!.RequiredCourseCount.Should().Be(3);
    }

    [Fact]
    public async Task CreatePool_when_group_faculty_differs_returns_bad_request()
    {
        // Arrange — Hukuk fakültesi scope'lu grup; müfredat Mühendislik/BLG'ye ait.
        var group = new ElectiveGroup
        {
            Name = "TE Hukuk",
            Code = "TE-LAW-POOL",
            ElectiveSubtype = ElectiveSubtype.TechnicalElective,
            FacultyId = FacultyLawId,
            DepartmentId = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.ElectiveGroups.Add(group);
        await _context.SaveChangesAsync();

        var controller = CreateCurriculumController();
        var req = new CurriculumController.CreatePoolRequest(
            ElectiveGroupId: group.Id,
            SuggestedSemesterStart: 5,
            SuggestedSemesterEnd: 8,
            RequiredCourseCount: 3,
            RequiredEcts: 9);

        // Act
        var result = await controller.CreatePool(CurriculumId, req, CancellationToken.None);

        // Assert
        MessageOf(result).Should().Contain("farklı bir fakülteye ait");
    }

    [Fact]
    public async Task CreatePool_when_group_department_differs_returns_bad_request()
    {
        // Arrange — bölüm scope'lu (Eng/EEM) grup; müfredat Eng/BLG'ye ait → bölüm uyuşmazlığı.
        var group = new ElectiveGroup
        {
            Name = "TE EEM",
            Code = "TE-EEM-POOL",
            ElectiveSubtype = ElectiveSubtype.TechnicalElective,
            FacultyId = FacultyEngId,
            DepartmentId = DeptElecEngId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.ElectiveGroups.Add(group);
        await _context.SaveChangesAsync();

        var controller = CreateCurriculumController();
        var req = new CurriculumController.CreatePoolRequest(
            ElectiveGroupId: group.Id,
            SuggestedSemesterStart: 5,
            SuggestedSemesterEnd: 8,
            RequiredCourseCount: 3,
            RequiredEcts: 9);

        // Act
        var result = await controller.CreatePool(CurriculumId, req, CancellationToken.None);

        // Assert
        MessageOf(result).Should().Contain("farklı bir bölüme ait");
    }

    public void Dispose() => _context.Dispose();
}
