using Microsoft.AspNetCore.Identity;
using SIS.Domain.Entities;
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
        await SeedTimeSlotsAsync(context);
        await SeedConstraintDefinitionsAsync(context);
        await SeedEvaluationQuestionsAsync(context);
        await SeedConstraintProfilesAsync(context);
    }
}
