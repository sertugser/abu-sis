using Microsoft.EntityFrameworkCore;

namespace SIS.Infrastructure.Persistence.Extensions;

/// <summary>
/// Scope query filter'larını bilinçli şekilde bypass etmek için kullanılır.
/// </summary>
public static class QueryFilterExtensions
{
    public static IQueryable<T> WithoutScope<T>(this IQueryable<T> source) where T : class =>
        source.IgnoreQueryFilters();
}
