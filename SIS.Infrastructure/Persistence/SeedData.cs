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
        await SeedUsersAsync(context, userManager);
        await SeedInstructorAvailabilitiesAsync(context);
        await SeedRegistrationsAsync(context);
    }
}
