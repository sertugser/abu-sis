using Microsoft.AspNetCore.Identity;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence.Seeds;
using SIS.Infrastructure.Services.CalendarGenerator;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    public static async Task SeedAsync(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<int>> roleManager)
    {
        await SeedRolesAsync(roleManager);
        await SeedAcademicStructureAsync(context);
        await SeedCoursesAsync(context);
        await SeedTermsAndClassroomsAsync(context);
        await new CalendarTemplateService(context).EnsureDefaultsAsync();
        await SeedUsersAsync(context, userManager);
        await SeedRegistrationsAsync(context);    // Geçmiş dönem transkript verileri (aktif döneme dokunmaz)
        // SeedSectionSchedulesAsync devre dışı — schedule'lar scheduling sistemi tarafından oluşturulmalı
        // SystemConfig önce seed edilmeli: TimeSlot generate'i Schedule.* anahtarlarını okuyor (özellikle FaceToFaceCutoffTime + WeekendOnlineOnly için AllowedMode damgalama)
        await SeedSystemConfigAsync(context);
        await SeedWebModuleSettingsAsync(context);
        await SeedAnnouncementCategoriesAsync(context);
        await SeedTimeSlotsAsync(context);
        await SeedConstraintDefinitionsAsync(context);
        await SeedEvaluationQuestionsAsync(context);

        // ── Course lookups (Açma Nedeni + Ders Kategorisi) ────
        await CurriculumCourseOpeningReasonSeed.SeedAsync(context);
        await CourseCategorySeed.SeedAsync(context);
        await ElectiveSubtypeSeed.SeedAsync(context);
        await DocumentTypeSeed.SeedAsync(context);

        // ── RBAC Phase 1 (Sprint 1.4) ──────────────────────────
        // Sıra önemli: Permission → MenuStructure (perm lookup gerekli) → RolePermission (perm + rol lookup)
        await PermissionSeed.SeedAsync(context);
        await MenuStructureSeed.SeedAsync(context);
        await RolePermissionSeed.SeedAsync(context, roleManager);

        // ── RBAC Phase 2 (Permission Bundles) ───────────────────
        // MenuStructureSeed sonrası: bundle'lar menu modüllerinden derive ediliyor
        await PermissionBundleSeed.SeedAsync(context);

        // ── Rapor Motoru (Faz 1): handler'ı olan raporları idempotent ekle ──
        await ReportSeed.SeedAsync(context);
    }
}
