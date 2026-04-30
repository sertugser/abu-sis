using SIS.Application.Common.Services;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services;

public readonly record struct RegistrationEctsThresholds(int Below200, int From200To249, int Above250);

public static class SystemConfigRegistrationPolicyExtensions
{
    public const string EctsLimitBelow200Key = "Registration.EctsLimit.CgpaBelow200";
    public const string EctsLimit200To249Key = "Registration.EctsLimit.Cgpa200To249";
    public const string EctsLimitAbove250Key = "Registration.EctsLimit.CgpaAbove250";
    public const string RetentionThresholdKey = "GPA.RetentionThreshold";
    public const string MaxWithdrawalUndergraduateKey = "Withdrawal.MaxCount.Undergraduate";
    public const string MaxWithdrawalAssociateKey = "Withdrawal.MaxCount.Associate";

    public static async Task<RegistrationEctsThresholds> GetRegistrationEctsThresholdsAsync(
        this ISystemConfigProvider config,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var below200 = await config.GetIntAsync(EctsLimitBelow200Key, 30, ct);
        var from200To249 = await config.GetIntAsync(EctsLimit200To249Key, 42, ct);
        var above250 = await config.GetIntAsync(EctsLimitAbove250Key, 48, ct);

        return new RegistrationEctsThresholds(below200, from200To249, above250);
    }

    public static async Task<int> GetRegistrationEctsLimitAsync(
        this ISystemConfigProvider config,
        decimal cgpa,
        CancellationToken ct = default)
    {
        var thresholds = await config.GetRegistrationEctsThresholdsAsync(ct);
        return RegistrationRulePolicy.GetEctsLimit(
            cgpa,
            thresholds.Below200,
            thresholds.From200To249,
            thresholds.Above250);
    }

    public static Task<decimal> GetRetentionThresholdAsync(
        this ISystemConfigProvider config,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.GetDecimalAsync(RetentionThresholdKey, 1.70m, ct);
    }

    public static Task<int> GetMaxWithdrawalCountAsync(
        this ISystemConfigProvider config,
        DegreeLevel degreeLevel,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var isAssociate = degreeLevel == DegreeLevel.Associate;
        return config.GetIntAsync(
            isAssociate ? MaxWithdrawalAssociateKey : MaxWithdrawalUndergraduateKey,
            isAssociate ? 3 : 6,
            ct);
    }
}
