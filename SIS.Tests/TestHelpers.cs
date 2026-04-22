using SIS.Application.Common.Time;

namespace SIS.Tests;

public sealed class FakeClock : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;
}
