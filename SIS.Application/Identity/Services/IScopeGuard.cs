using SIS.Domain.Identity;

namespace SIS.Application.Identity.Services;

public interface IScopeGuard
{
    void EnsureCanWrite(ScopeKind kind, int targetRefId);
}
