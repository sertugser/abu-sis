namespace SIS.Application.Identity.DTOs;

public class PermissionBundleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsSystemDefault { get; set; }
    public int PermissionCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PermissionBundleDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsSystemDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<int> PermissionIds { get; set; } = new();
}

public class CreatePermissionBundleDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public List<int> PermissionIds { get; set; } = new();
}

public class UpdatePermissionBundleDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public List<int> PermissionIds { get; set; } = new();
}

public class ApplyBundleToRoleDto
{
    public int BundleId { get; set; }
}

/// <summary>
/// Bir role uygulanmış paketin durumu. <see cref="IsStale"/> true ise paketin güncel
/// içeriği, role uygulanmış (snapshot) haliyle aynı değil → "Yeniden Uygula" gerekir.
/// </summary>
public class AppliedBundleDto
{
    public int BundleId { get; set; }
    public bool IsStale { get; set; }
}
