using Microsoft.AspNetCore.Identity;

namespace SIS.Infrastructure.Persistence;

public class ApplicationUser : IdentityUser<int>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime? PasswordChangedAtUtc { get; set; }
}
