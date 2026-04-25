namespace SIS.Application.Identity.DTOs;

public class MenuModuleDto
{
    public int Id { get; set; }
    /// <summary>Stabil string identifier (örn. "sik-kullanilanlar"). Frontend bunu module key olarak kullanır.</summary>
    public string Slug { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public List<MenuItemDto> Items { get; set; } = new();
    public List<MenuModuleDto> Children { get; set; } = new();
}
