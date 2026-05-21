using SIS.Application.Identity.DTOs;

namespace SIS.Application.Identity.Services;

public interface IPermissionDelegationService
{
    Task<IReadOnlyList<PermissionDelegationDto>> ListAsync(
        bool includeInactive,
        int? delegatorUserId,
        int? delegateeUserId,
        CancellationToken ct = default);

    Task<PermissionDelegationDto> GetAsync(int id, CancellationToken ct = default);

    Task<PermissionDelegationDto> CreateAsync(
        CreateDelegationDto dto,
        int createdByUserId,
        CancellationToken ct = default);

    Task<PermissionDelegationDto> RevokeAsync(
        int id,
        RevokeDelegationDto dto,
        int revokedByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Kullanıcının şu an aktif bir vekâletle devraldığı rol isimleri (distinct).
    /// Panel switcher ve menu filter "kendi rolleri ∪ vekâlet rolleri" set'ine bunu eklemek için kullanır.
    /// SystemAdministrator her zaman çıkarılır (panel switcher'a eklemek anlamsız + güvenlik).
    /// </summary>
    Task<IReadOnlyList<string>> GetActiveDelegatedRoleNamesAsync(int userId, CancellationToken ct = default);
}
