using Microsoft.AspNetCore.Mvc.Filters;

namespace SIS.API.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class SensitiveDataAccessAttribute : Attribute, IFilterMetadata
{
    public string? EntityIdRouteParam { get; set; }
    public string EntityType { get; set; } = "Student";

    public SensitiveDataAccessAttribute() { }

    public SensitiveDataAccessAttribute(string entityType)
    {
        EntityType = entityType;
    }
}
