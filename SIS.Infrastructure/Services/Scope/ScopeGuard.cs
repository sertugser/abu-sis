using SIS.Application.Identity.Exceptions;
using SIS.Application.Identity.Services;
using SIS.Domain.Identity;

namespace SIS.Infrastructure.Services.Scope;

public class ScopeGuard : IScopeGuard
{
    private readonly ICurrentUserScope _scope;

    public ScopeGuard(ICurrentUserScope scope)
    {
        _scope = scope;
    }

    public void EnsureCanWrite(ScopeKind kind, int targetRefId)
    {
        if (!_scope.IsActive)
        {
            return;
        }

        switch (kind)
        {
            case ScopeKind.Faculty:
                if (!_scope.FacultyIds.Contains(targetRefId))
                {
                    var scopeDesc = _scope.FacultyIds.Count > 0
                        ? string.Join(",", _scope.FacultyIds)
                        : "none";
                    throw new ScopeViolationException(
                        $"Target faculty {targetRefId} is outside user's scope (user faculties: {scopeDesc}).");
                }

                return;

            default:
                throw new NotSupportedException($"ScopeKind {kind} is not supported in Phase 2.");
        }
    }
}
