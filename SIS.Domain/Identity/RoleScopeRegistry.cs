namespace SIS.Domain.Identity;

/// <summary>
/// Scope-bound rollerin merkezi listesi. Phase 2'de sadece Dean kapsamlıdır.
/// </summary>
public static class RoleScopeRegistry
{
    private static readonly HashSet<string> ScopeBoundRoles = new(StringComparer.Ordinal)
    {
        "Dean",
    };

    public static bool IsScopeBound(string roleName) =>
        ScopeBoundRoles.Contains(roleName);

    public static bool AnyScopeBound(IEnumerable<string> roleNames)
    {
        foreach (var roleName in roleNames)
        {
            if (ScopeBoundRoles.Contains(roleName))
            {
                return true;
            }
        }

        return false;
    }
}
