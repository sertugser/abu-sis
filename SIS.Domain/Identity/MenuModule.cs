namespace SIS.Domain.Identity;

public class MenuModule
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    /// <summary>
    /// Stabil string identifier — frontend'in 'sik-kullanilanlar', 'yonetim' gibi key'lerde
    /// güvenle kullanması için. Label değişse bile slug aynı kalır (immutable kontrat).
    /// </summary>
    public string Slug { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>
    /// Bu modülün hangi rollere gözükeceği (CSV, ör. "Student,Instructor"). Null ise herkes görür (default).
    /// SystemAdmin shortcut bu kuralı BYPASS ETMEZ — modül-bazlı role filter PermissionService
    /// shortcut'ından bağımsız çalışır (admin için modül gizleme amaçlı).
    /// </summary>
    public string? AllowedRolesCsv { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public MenuModule? Parent { get; set; }
    public ICollection<MenuModule> Children { get; set; } = new List<MenuModule>();
    public ICollection<MenuItem> Items { get; set; } = new List<MenuItem>();
}
