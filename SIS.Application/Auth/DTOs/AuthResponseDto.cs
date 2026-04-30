namespace SIS.Application.Auth.DTOs;

public class AuthResponseDto
{
    public bool Success { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
    public bool RequiresPasswordChange { get; set; }
    public DateTime? PasswordExpiresAt { get; set; }
    public string DefaultLanguage { get; set; } = "tr-TR";
    public string? Error { get; set; }
}
