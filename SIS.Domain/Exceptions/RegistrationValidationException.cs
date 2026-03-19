namespace SIS.Domain.Exceptions;

public class RegistrationValidationException : DomainException
{
    public IReadOnlyList<string> Errors { get; }
    public RegistrationValidationException(IEnumerable<string> errors)
        : base("Course registration validation failed.")
    {
        Errors = errors.ToList().AsReadOnly();
    }
}
