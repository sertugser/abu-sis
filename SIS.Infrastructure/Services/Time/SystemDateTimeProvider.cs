using SIS.Application.Common.Time;

namespace SIS.Infrastructure.Services.Time;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
