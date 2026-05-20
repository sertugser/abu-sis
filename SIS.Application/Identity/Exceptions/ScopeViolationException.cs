namespace SIS.Application.Identity.Exceptions;

public class ScopeViolationException : Exception
{
    public ScopeViolationException()
        : base("Scope violation: target resource is outside user's scope.")
    {
    }

    public ScopeViolationException(string message)
        : base(message)
    {
    }
}
