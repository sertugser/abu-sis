namespace SIS.Domain.Exceptions;

public class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, object id)
        : base($"{entityName} with id '{id}' was not found.") { }

    public EntityNotFoundException(string entityName, object id, Exception inner)
        : base($"{entityName} with id '{id}' was not found.", inner) { }
}
