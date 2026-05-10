using FluentAssertions;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Tests.Application.Constraints;

public class ConstraintCategoryOverrideTests : SchedulingTestBase
{
    [Fact]
    public async Task ValidateExistingAsync_WhenHardConstraintIsConfiguredSoft_DoesNotReturnHardConflict()
    {
        Context.ConstraintDefinitions.Add(new ConstraintDefinition
        {
            Code = "H1",
            Name = "Classroom conflict",
            Category = "Soft",
            Scope = "Scheduling",
            IsEnabled = true
        });
        Context.SaveChanges();

        var classroom = AddClassroom(1, "D101", 40);
        var course1 = AddCourse(1, "CENG701", theoryHours: 1);
        var course2 = AddCourse(2, "CENG702", theoryHours: 1);
        AddSection(1, course1.Id, AddInstructor(1).Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0));
        AddSection(2, course2.Id, AddInstructor(2).Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 30, 10, 30));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == ClassroomConflict);
        result.Notes.Should().Contain(note => note.Contains("Derslik"));
    }

    [Fact]
    public async Task ValidateExistingAsync_WhenSoftConstraintIsConfiguredHard_ReturnsSyntheticHardConflict()
    {
        Context.ConstraintDefinitions.Add(new ConstraintDefinition
        {
            Code = "S1",
            Name = "Occupancy",
            Category = "Hard",
            Scope = "Scheduling",
            IsEnabled = true
        });
        Context.SaveChanges();
        SetSystemConfig("SoftConstraint.S1.Enabled", "true");

        var classroom = AddClassroom(1, "D201", 100);
        var course = AddCourse(1, "CENG703", theoryHours: 1);
        AddSection(1, course.Id, AddInstructor(1).Id, classroom.Id, 20,
            (DayOfWeek.Monday, 9, 0, 10, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == "S1");
    }

    [Fact]
    public async Task ValidateExistingAsync_PreservesDistributionConstraintsInFilteredSnapshots()
    {
        var classroom = AddClassroom(1, "D301", 40);
        var course1 = AddCourse(1, "CENG704", theoryHours: 1);
        var course2 = AddCourse(2, "CENG705", theoryHours: 1);
        AddSection(1, course1.Id, AddInstructor(1).Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0));
        AddSection(2, course2.Id, AddInstructor(2).Id, classroom.Id, 30,
            (DayOfWeek.Tuesday, 9, 0, 10, 0));

        Context.DistributionConstraints.Add(new DistributionConstraint
        {
            Id = 1,
            TermId = ActiveTermId,
            DistributionType = DistributionType.SameDay,
            PreferenceLevel = PreferenceLevel.Unavailable,
            Courses =
            {
                new DistributionConstraintCourse { Id = 1, CourseId = course1.Id, SortOrder = 1 },
                new DistributionConstraintCourse { Id = 2, CourseId = course2.Id, SortOrder = 2 },
            }
        });
        Context.SaveChanges();

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == "DistributionConstraint");
    }
}
