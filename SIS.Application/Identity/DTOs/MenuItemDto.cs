namespace SIS.Application.Identity.DTOs;

public class MenuItemDto
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool OpenInNewTab { get; set; }
}
