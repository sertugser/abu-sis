using Microsoft.AspNetCore.Mvc.Filters;

namespace SIS.API.Filters;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class SensitiveDataAccessAttribute : Attribute, IFilterMetadata
{
    public string? EntityIdRouteParam { get; set; }
    public string EntityType { get; set; } = "Student";
}
