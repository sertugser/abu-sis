namespace SIS.Application.UserPreferences.DTOs;

/// <summary>
/// Kullanici tercih bilgisi. HasCustomized=false ise kullanici henuz kayit atmamis demektir
/// (frontend bu durumda role-default favorilere duser).
/// </summary>
public sealed record UserPreferenceDto(string[] Favorites, bool HasCustomized);

public sealed record UpdateUserPreferenceRequest(string[] Favorites);
