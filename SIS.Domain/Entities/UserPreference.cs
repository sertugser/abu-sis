namespace SIS.Domain.Entities;

/// <summary>
/// Kullanici-bazli UI tercihleri (sidebar favorileri vb.).
/// FavoritesJson: Route stringlerinin JSON dizisi. Max uzunluk servis katmaninda validate edilir.
/// </summary>
public class UserPreference
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string FavoritesJson { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; }
}
