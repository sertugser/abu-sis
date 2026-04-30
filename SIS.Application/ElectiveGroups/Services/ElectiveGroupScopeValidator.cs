using SIS.Domain.Entities;

namespace SIS.Application.ElectiveGroups.Services;

public sealed record ScopeValidationResult(bool IsValid, string? ErrorMessage);

public interface IElectiveGroupScopeValidator
{
    ScopeValidationResult CanBind(ElectiveGroup group, int facultyId, int departmentId);
}

public sealed class ElectiveGroupScopeValidator : IElectiveGroupScopeValidator
{
    public ScopeValidationResult CanBind(ElectiveGroup group, int facultyId, int departmentId)
    {
        // Okul scope (her iki null) → her zaman geçerli
        if (!group.FacultyId.HasValue && !group.DepartmentId.HasValue)
            return new(true, null);

        // Fakülte scope → müfredatın fakültesi eşleşmeli
        if (group.FacultyId.HasValue && group.FacultyId != facultyId)
            return new(false, $"Bu grup '{group.Name}' farklı bir fakülteye ait — müfredata bağlanamaz.");

        // Bölüm scope → müfredatın bölümü eşleşmeli
        if (group.DepartmentId.HasValue && group.DepartmentId != departmentId)
            return new(false, $"Bu grup '{group.Name}' farklı bir bölüme ait — müfredata bağlanamaz.");

        return new(true, null);
    }
}
