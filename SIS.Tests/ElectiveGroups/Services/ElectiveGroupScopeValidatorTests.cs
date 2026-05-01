using FluentAssertions;
using SIS.Application.ElectiveGroups.Services;
using SIS.Domain.Entities;
using Xunit;

namespace SIS.Tests.ElectiveGroups.Services;

public class ElectiveGroupScopeValidatorTests
{
    private readonly ElectiveGroupScopeValidator _sut = new();

    [Fact]
    public void School_scope_group_can_bind_to_any_curriculum()
    {
        var group = new ElectiveGroup { Name = "OC-I", FacultyId = null, DepartmentId = null };

        var result = _sut.CanBind(group, facultyId: 1, departmentId: 10);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Faculty_scope_group_can_bind_to_same_faculty_curriculum()
    {
        var group = new ElectiveGroup { Name = "MUH-TE-I", FacultyId = 1, DepartmentId = null };

        var result = _sut.CanBind(group, facultyId: 1, departmentId: 10);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Faculty_scope_group_cannot_bind_to_different_faculty()
    {
        var group = new ElectiveGroup { Name = "HUK-TE-I", FacultyId = 2, DepartmentId = null };

        var result = _sut.CanBind(group, facultyId: 1, departmentId: 10);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("fakülte");
    }

    [Fact]
    public void Department_scope_group_can_bind_to_same_department()
    {
        var group = new ElectiveGroup { Name = "BLG-TE-I", FacultyId = 1, DepartmentId = 10 };

        var result = _sut.CanBind(group, facultyId: 1, departmentId: 10);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Department_scope_group_cannot_bind_to_different_department()
    {
        var group = new ElectiveGroup { Name = "BLG-TE-I", FacultyId = 1, DepartmentId = 10 };

        var result = _sut.CanBind(group, facultyId: 1, departmentId: 20);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bölüm");
    }
}
