namespace SIS.Application.Identity.DTOs;

/// <summary>
/// Admin Menu Editor — recursive module tree (parent + children + items + permission keys).
/// Read shape; write side uses Create/Update/Reorder DTO'ları.
/// </summary>
public class MenuModuleAdminDto
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public string? AllowedRolesCsv { get; set; }
    public List<MenuItemAdminDto> Items { get; set; } = new();
    public List<MenuModuleAdminDto> Children { get; set; } = new();
}

public class MenuItemAdminDto
{
    public int Id { get; set; }
    public int ModuleId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool OpenInNewTab { get; set; }
    public bool IsActive { get; set; }
    /// <summary>Bu item'a OR mantığıyla bağlı permission key'leri.</summary>
    public List<string> PermissionKeys { get; set; } = new();
}

public class CreateMenuModuleDto
{
    public int? ParentId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? AllowedRolesCsv { get; set; }
}

public class UpdateMenuModuleDto
{
    public string Label { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? AllowedRolesCsv { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateMenuItemDto
{
    public int ModuleId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool OpenInNewTab { get; set; }
    /// <summary>Item'ın çağrılması için OR mantığıyla yeterli olan permission key'leri.</summary>
    public List<string> PermissionKeys { get; set; } = new();
}

public class UpdateMenuItemDto
{
    public string Label { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public bool OpenInNewTab { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> PermissionKeys { get; set; } = new();
}

/// <summary>
/// Sürükle-bırak sonucu sıralama. Item çocuk olduğu modülün altında SortOrder güncellenir.
/// </summary>
public class ReorderModulesDto
{
    /// <summary>(ModuleId, NewSortOrder) çiftleri. Tüm girişler aynı parent altındadır.</summary>
    public List<ReorderEntry> Entries { get; set; } = new();
    /// <summary>Yeni parent (null = top-level). Tüm modüllerin parent'ı buraya taşınır.</summary>
    public int? ParentId { get; set; }
}

public class ReorderItemsDto
{
    public int ModuleId { get; set; }
    public List<ReorderEntry> Entries { get; set; } = new();
}

public class ReorderEntry
{
    public int Id { get; set; }
    public int SortOrder { get; set; }
}
