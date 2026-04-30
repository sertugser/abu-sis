using SIS.Application.Common.Services;
using SIS.Application.Common.Time;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public sealed class PasswordRotationService
{
    private const string PasswordRotationEnabledKey = "Security.PasswordRotationEnabled";
    private const string PasswordRotationMonthsKey = "Security.PasswordRotationMonths";

    private readonly ISystemConfigProvider _config;
    private readonly IDateTimeProvider _clock;

    public PasswordRotationService(ISystemConfigProvider config, IDateTimeProvider clock)
    {
        _config = config;
        _clock = clock;
    }

    public async Task<PasswordRotationStatus> GetStatusAsync(
        ApplicationUser user,
        CancellationToken ct = default)
    {
        var isEnabled = await _config.GetBoolAsync(PasswordRotationEnabledKey, true, ct);
        var rotationMonths = Math.Max(1, await _config.GetIntAsync(PasswordRotationMonthsKey, 6, ct));

        if (!isEnabled)
        {
            return new PasswordRotationStatus(
                IsEnabled: false,
                RequiresPasswordChange: false,
                PasswordChangedAtUtc: user.PasswordChangedAtUtc,
                PasswordExpiresAtUtc: null,
                RotationMonths: rotationMonths);
        }

        var passwordChangedAtUtc = user.PasswordChangedAtUtc ?? _clock.UtcNow;
        var passwordExpiresAtUtc = passwordChangedAtUtc.AddMonths(rotationMonths);

        return new PasswordRotationStatus(
            IsEnabled: true,
            RequiresPasswordChange: _clock.UtcNow >= passwordExpiresAtUtc,
            PasswordChangedAtUtc: passwordChangedAtUtc,
            PasswordExpiresAtUtc: passwordExpiresAtUtc,
            RotationMonths: rotationMonths);
    }

    public void MarkPasswordChanged(ApplicationUser user)
    {
        user.PasswordChangedAtUtc = _clock.UtcNow;
    }
}

public sealed record PasswordRotationStatus(
    bool IsEnabled,
    bool RequiresPasswordChange,
    DateTime? PasswordChangedAtUtc,
    DateTime? PasswordExpiresAtUtc,
    int RotationMonths);
