namespace SIS.Application.Common.Time;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
