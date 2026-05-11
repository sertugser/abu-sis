namespace SIS.Application.Scheduling.Services;

public class ScheduleImportSessionInvalidException : InvalidOperationException
{
    public ScheduleImportSessionInvalidException(string message) : base(message)
    {
    }
}

public sealed class ScheduleImportSessionForbiddenException : ScheduleImportSessionInvalidException
{
    public ScheduleImportSessionForbiddenException(string message) : base(message)
    {
    }
}
