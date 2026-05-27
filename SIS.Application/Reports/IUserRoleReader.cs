namespace SIS.Application.Reports;

/// <summary>UserManager'ı doğrudan bağlamadan rol okuma (test edilebilirlik).</summary>
public interface IUserRoleReader
{
    Task<IReadOnlyCollection<string>> GetRolesAsync(int userId, CancellationToken ct = default);
}
