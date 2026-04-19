using Microsoft.AspNetCore.Identity;
using SIS.Domain.Entities;

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
        await SeedCalendarEventsAsync(context);
        await SeedUsersAsync(context, userManager);
        await SeedRegistrationsAsync(context);    // Geçmiş dönem transkript verileri (aktif döneme dokunmaz)
        // SeedSectionSchedulesAsync devre dışı — schedule'lar scheduling sistemi tarafından oluşturulmalı
        await SeedTimeSlotsAsync(context);
        await SeedSystemConfigAsync(context);
        await SeedConstraintDefinitionsAsync(context);
        await SeedEvaluationQuestionsAsync(context);
    }
}
