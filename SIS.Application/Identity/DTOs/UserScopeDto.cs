using SIS.Domain.Identity;

namespace SIS.Application.Identity.DTOs;

public record UserScopeDto(
    int UserId,
    ScopeKind Kind,
    int? FacultyId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SetUserScopeDto(
    ScopeKind Kind,
    int? FacultyId);
