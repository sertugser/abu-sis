using Microsoft.AspNetCore.Identity;
using SIS.Application.Reports;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Reports;

public sealed class UserRoleReader : IUserRoleReader
{
    private readonly UserManager<ApplicationUser> _userManager;
    public UserRoleReader(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<IReadOnlyCollection<string>> GetRolesAsync(int userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Array.Empty<string>();
        var roles = await _userManager.GetRolesAsync(user);
        return roles.ToArray();
    }
}
