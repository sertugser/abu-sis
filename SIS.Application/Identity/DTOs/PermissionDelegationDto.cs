namespace SIS.Application.Identity.DTOs;

public record PermissionDelegationDto(
    int Id,
    int DelegatorUserId,
    string DelegatorUserName,
    string DelegatorFullName,
    int DelegateeUserId,
    string DelegateeUserName,
    string DelegateeFullName,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string Reason,
    DateTime? RevokedAtUtc,
    int? RevokedByUserId,
    string? RevokeReason,
    DateTime CreatedAtUtc,
    int CreatedByUserId,
    string Status); // "Active" | "Pending" | "Expired" | "Revoked"

public record CreateDelegationDto(
    int DelegatorUserId,
    int DelegateeUserId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string Reason);

public record RevokeDelegationDto(string? Reason);
