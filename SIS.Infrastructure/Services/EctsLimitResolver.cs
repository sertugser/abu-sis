using SIS.Application.Common.Services;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Services;

/// <inheritdoc />
public sealed class EctsLimitResolver : IEctsLimitResolver
{
    private const int DefaultBelow200 = 30;
    private const int Default200To249 = 42;
    private const int DefaultAbove250 = 48;

    private readonly ISystemConfigProvider _config;

    public EctsLimitResolver(ISystemConfigProvider config) => _config = config;

    public async Task<int> ResolveAsync(decimal cgpa, Curriculum? curriculum, CancellationToken ct = default)
    {
        var below200 = curriculum?.EctsLimitBelow200
            ?? await _config.GetIntAsync("Registration.EctsLimit.CgpaBelow200", DefaultBelow200, ct);
        var mid = curriculum?.EctsLimit200To249
            ?? await _config.GetIntAsync("Registration.EctsLimit.Cgpa200To249", Default200To249, ct);
        var above250 = curriculum?.EctsLimitAbove250
            ?? await _config.GetIntAsync("Registration.EctsLimit.CgpaAbove250", DefaultAbove250, ct);

        return cgpa >= 2.50m ? above250
             : cgpa >= 2.00m ? mid
             : below200;
    }
}
