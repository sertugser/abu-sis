namespace SIS.Application.Integration;

/// <summary>
/// Boundary abstraction over the NVİ (national identity) verification system.
/// Implementations are selected by configuration mode (Mock / Disabled / Live);
/// the mock returns deterministic fake data flagged <c>IsMock = true</c>.
/// </summary>
public interface IExternalIdentityService
{
    Task<IdentityVerificationResult> VerifyAsync(int studentId, CancellationToken ct = default);
}
