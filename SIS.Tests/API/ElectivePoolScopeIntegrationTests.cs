using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Controllers;
using SIS.Application.ElectiveGroups.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Tests.TestHelpers;

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
        _context.SeedElectiveSubtypeDefaults();
    }

    private ElectiveGroupController CreateGroupController() => new(_context);

    private CurriculumController CreateCurriculumController() =>
        new(_context, new ElectiveGroupScopeValidator(), new CourseStatisticsService(_context));

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
            ElectiveSubtypeId: TestSeedExtensions.TechnicalElectiveId,
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
        dto.ElectiveSubtypeId.Should().Be(TestSeedExtensions.TechnicalElectiveId);
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
            ElectiveSubtypeId: TestSeedExtensions.NonDepartmentElectiveId,
            FacultyId: null,
            DepartmentId: DeptCompEngId);

        // Act
        var result = await controller.Create(req, CancellationToken.None);

        // Assert
        MessageOf(result).Should().Contain("Bölüm seçildiğinde fakülte");
    }

    [Fact]
    public async Task Create_UniversityElective_with_DepartmentId_is_rejected_by_earlier_scope_guards()
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
            ElectiveSubtypeId: TestSeedExtensions.UniversityElectiveId,
            FacultyId: null,
            DepartmentId: DeptCompEngId);
        var reqWithFaculty = new ElectiveGroupController.CreateElectiveGroupRequest(
            Name: "OC + Bölüm (with faculty)",
            Code: "OC-WITHDEPT-WF",
            ElectiveSubtypeId: TestSeedExtensions.UniversityElectiveId,
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
            ElectiveSubtypeId: TestSeedExtensions.TechnicalElectiveId,
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
            ElectiveSubtypeId = TestSeedExtensions.TechnicalElectiveId,
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
            ElectiveSubtypeId = TestSeedExtensions.TechnicalElectiveId,
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
    public async Task Update_changes_scope_when_no_bindings_exist()
    {
        // Arrange — fakülte scope'lu (Eng) grup, hiç müfredat bağlantısı yok.
        var group = new ElectiveGroup
        {
            Name = "MUH-TE-I",
            Code = "MUH-TE-I",
            ElectiveSubtypeId = TestSeedExtensions.TechnicalElectiveId,
            FacultyId = FacultyEngId,
            DepartmentId = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.ElectiveGroups.Add(group);
        await _context.SaveChangesAsync();

        var controller = CreateGroupController();
        var req = new ElectiveGroupController.UpdateElectiveGroupRequest(
            FacultyId: FacultyEngId,
            DepartmentId: DeptCompEngId);

        // Act
        var result = await controller.Update(group.Id, req, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var reloaded = await _context.ElectiveGroups.AsNoTracking().FirstAsync(eg => eg.Id == group.Id);
        reloaded.FacultyId.Should().Be(FacultyEngId);
        reloaded.DepartmentId.Should().Be(DeptCompEngId);
    }

    [Fact]
    public async Task Update_scope_change_with_existing_curriculum_bindings_returns_bad_request()
    {
        // Arrange — fakülte scope'lu grup + mevcut müfredat bağlantısı.
        var group = new ElectiveGroup
        {
            Name = "TE bağlı",
            Code = "TE-BOUND",
            ElectiveSubtypeId = TestSeedExtensions.TechnicalElectiveId,
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
            ElectiveSubtypeId = TestSeedExtensions.TechnicalElectiveId,
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
            SlotName: "TE-1",
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
        pool.SlotName.Should().Be("TE-1");
    }

    [Fact]
    public async Task CreatePool_when_group_faculty_differs_returns_bad_request()
    {
        // Arrange — Hukuk fakültesi scope'lu grup; müfredat Mühendislik/BLG'ye ait.
        var group = new ElectiveGroup
        {
            Name = "TE Hukuk",
            Code = "TE-LAW-POOL",
            ElectiveSubtypeId = TestSeedExtensions.TechnicalElectiveId,
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
            SlotName: "TE-1",
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
            ElectiveSubtypeId = TestSeedExtensions.TechnicalElectiveId,
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
            SlotName: "TE-1",
            SuggestedSemesterStart: 5,
            SuggestedSemesterEnd: 8,
            RequiredCourseCount: 3,
            RequiredEcts: 9);

        // Act
        var result = await controller.CreatePool(CurriculumId, req, CancellationToken.None);

        // Assert
        MessageOf(result).Should().Contain("farklı bir bölüme ait");
    }

    // ── Group 4: CreatePool SlotName validation (Faz B) ───────────────

    [Fact]
    public async Task CreatePool_with_empty_slot_name_returns_bad_request()
    {
        // Arrange — scope-valid (faculty match) grup; SlotName boş → reddedilmeli.
        var group = new ElectiveGroup
        {
            Name = "TE Eng (empty slot test)",
            Code = "TE-ENG-EMPTY",
            ElectiveSubtypeId = TestSeedExtensions.TechnicalElectiveId,
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
            SlotName: "",
            SuggestedSemesterStart: 5,
            SuggestedSemesterEnd: 5,
            RequiredCourseCount: 1,
            RequiredEcts: 6);

        // Act
        var result = await controller.CreatePool(CurriculumId, req, CancellationToken.None);

        // Assert
        MessageOf(result).Should().Contain("Slot adı boş olamaz");
    }

    [Fact]
    public async Task CreatePool_with_duplicate_slot_name_returns_bad_request()
    {
        // Arrange — aynı (curriculum, group, slotName) ikinci kez eklenmeye çalışılırsa reddedilir.
        var group = new ElectiveGroup
        {
            Name = "TE Eng (dup slot test)",
            Code = "TE-ENG-DUP",
            ElectiveSubtypeId = TestSeedExtensions.TechnicalElectiveId,
            FacultyId = FacultyEngId,
            DepartmentId = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.ElectiveGroups.Add(group);
        await _context.SaveChangesAsync();

        // Önce TE-1 slotunu yaratıp commit et.
        _context.CurriculumElectivePools.Add(new CurriculumElectivePool
        {
            CurriculumId = CurriculumId,
            ElectiveGroupId = group.Id,
            SlotName = "TE-1",
            SuggestedSemesterStart = 5,
            SuggestedSemesterEnd = 8,
            RequiredCourseCount = 1,
            RequiredEcts = 6,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var controller = CreateCurriculumController();
        var req = new CurriculumController.CreatePoolRequest(
            ElectiveGroupId: group.Id,
            SlotName: "TE-1",
            SuggestedSemesterStart: 5,
            SuggestedSemesterEnd: 8,
            RequiredCourseCount: 1,
            RequiredEcts: 6);

        // Act
        var result = await controller.CreatePool(CurriculumId, req, CancellationToken.None);

        // Assert
        MessageOf(result).Should().Contain("'TE-1' adlı slot bu havuza zaten bağlı");
    }

    [Fact]
    public async Task CreatePool_with_different_slot_names_for_same_group_succeeds()
    {
        // Arrange — aynı grupta farklı slot adlarıyla iki havuz oluşturulabilmeli (TE-1, TE-2).
        var group = new ElectiveGroup
        {
            Name = "TE Eng (multi slot)",
            Code = "TE-ENG-MULTI",
            ElectiveSubtypeId = TestSeedExtensions.TechnicalElectiveId,
            FacultyId = FacultyEngId,
            DepartmentId = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.ElectiveGroups.Add(group);
        await _context.SaveChangesAsync();

        var controller = CreateCurriculumController();

        var req1 = new CurriculumController.CreatePoolRequest(
            ElectiveGroupId: group.Id,
            SlotName: "TE-1",
            SuggestedSemesterStart: 5,
            SuggestedSemesterEnd: 5,
            RequiredCourseCount: 1,
            RequiredEcts: 6);
        var req2 = new CurriculumController.CreatePoolRequest(
            ElectiveGroupId: group.Id,
            SlotName: "TE-2",
            SuggestedSemesterStart: 6,
            SuggestedSemesterEnd: 6,
            RequiredCourseCount: 1,
            RequiredEcts: 6);

        // Act
        var result1 = await controller.CreatePool(CurriculumId, req1, CancellationToken.None);
        var result2 = await controller.CreatePool(CurriculumId, req2, CancellationToken.None);

        // Assert — her iki create de Ok döner ve DB'de iki kayıt olur.
        result1.Should().BeOfType<OkObjectResult>();
        result2.Should().BeOfType<OkObjectResult>();

        var pools = await _context.CurriculumElectivePools.AsNoTracking()
            .Where(p => p.CurriculumId == CurriculumId && p.ElectiveGroupId == group.Id)
            .OrderBy(p => p.SlotName)
            .ToListAsync();
        pools.Should().HaveCount(2);
        pools.Select(p => p.SlotName).Should().BeEquivalentTo(new[] { "TE-1", "TE-2" });
    }

    // ── Group 5: EctsPerCourse round-trip (Faz B follow-ups) ──────────

    [Fact]
    public async Task CreatePool_with_EctsPerCourse_persists_value()
    {
        // Arrange — scope-valid (faculty match) grup; EctsPerCourse=3 ile create.
        var group = new ElectiveGroup
        {
            Name = "MUH-TE-I",
            Code = "MUH-TE-I",
            ElectiveSubtypeId = TestSeedExtensions.TechnicalElectiveId,
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
            SlotName: "TE-1",
            SuggestedSemesterStart: 5,
            SuggestedSemesterEnd: 5,
            RequiredCourseCount: 1,
            RequiredEcts: 6,
            EctsPerCourse: 3);

        // Act
        var result = await controller.CreatePool(CurriculumId, req, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var pool = await _context.CurriculumElectivePools.AsNoTracking()
            .FirstAsync(p => p.CurriculumId == CurriculumId && p.SlotName == "TE-1");
        pool.EctsPerCourse.Should().Be(3);
    }

    [Fact]
    public async Task UpdatePool_with_ClearEctsPerCourse_sets_field_to_null()
    {
        // Arrange — EctsPerCourse=5 olan mevcut havuz; ClearEctsPerCourse=true ile null'a çek.
        var group = new ElectiveGroup
        {
            Name = "MUH-TE-I",
            Code = "MUH-TE-I",
            ElectiveSubtypeId = TestSeedExtensions.TechnicalElectiveId,
            FacultyId = FacultyEngId,
            DepartmentId = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.ElectiveGroups.Add(group);
        await _context.SaveChangesAsync();

        var pool = new CurriculumElectivePool
        {
            CurriculumId = CurriculumId,
            ElectiveGroupId = group.Id,
            SlotName = "TE-1",
            SuggestedSemesterStart = 5,
            SuggestedSemesterEnd = 5,
            RequiredCourseCount = 1,
            RequiredEcts = 6,
            EctsPerCourse = 5,
            CreatedAt = DateTime.UtcNow
        };
        _context.CurriculumElectivePools.Add(pool);
        await _context.SaveChangesAsync();

        var controller = CreateCurriculumController();
        var req = new CurriculumController.UpdatePoolRequest(
            ClearEctsPerCourse: true);

        // Act
        var result = await controller.UpdatePool(CurriculumId, pool.Id, req, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var reloaded = await _context.CurriculumElectivePools.AsNoTracking()
            .FirstAsync(p => p.Id == pool.Id);
        reloaded.EctsPerCourse.Should().BeNull();
    }

    public void Dispose() => _context.Dispose();
}
