using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Soft;

public class ClassroomTypeSoftEncoderTests
{
    private static SectionInput LabOnlySection() => new()
    {
        Id = 1, CourseId = 100, CourseCode = "X", CourseName = "X", SectionNumber = 1,
        Capacity = 40, TheoryHours = 0, LabHours = 2, TeachingMethod = TeachingMethod.FaceToFace
    };

    // Lab-only section + yalnız Lecture tipi oda → tip uyumsuz (lab Lab/Computer bekler).
    private static EncoderContext BuildContext(string h4Category)
    {
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "Lecture", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var section = LabOnlySection();
        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: 50);
        variables.CreateIntervalsForSection(section);
        variables.CreateRoomVariable(section.Id, ComponentType.Lab, classrooms, groupCount: section.LabGroupCount);

        return new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = new SolverRequest
            {
                TermId = 1, FacultyId = 1,
                Sections = new[] { section },
                Classrooms = classrooms,
                TimeSlots = new List<TimeSlotInput>(),
                ConstraintCategories = new Dictionary<string, string> { ["H4"] = h4Category },
                SoftThresholds = new SoftThresholds { H4Weight = 5 },
            },
            ConstraintFlags = new Dictionary<string, bool>(),
            Penalties = new List<LinearExpr>(),
        };
    }

    [Fact]
    public void Code_IsH4_AndIsSoft()
    {
        var encoder = new ClassroomTypeSoftEncoder();
        encoder.Code.Should().Be("H4");
        encoder.IsHard.Should().BeFalse();
    }

    [Fact]
    public void Encode_HardCategory_AddsNoPenalty()
    {
        // Default Hard → soft encoder no-op (hard encoder yasaklamayı uygular).
        var ctx = BuildContext("Hard");
        new ClassroomTypeSoftEncoder().Encode(ctx);
        ctx.Penalties.Should().BeEmpty();
    }

    [Fact]
    public void Encode_SoftCategory_WrongTypeRoom_AddsPenaltyAndStaysFeasible()
    {
        // Soft modda yasak yerine penalty: tip-uyumsuz oda kullanılır ama H4Weight ceza alır.
        // (Eskiden STUB'tı → hiç penalty üretmezdi; bu test artık gerçek soft davranışı doğrular.)
        var ctx = BuildContext("Soft");
        new ClassroomTypeSoftEncoder().Encode(ctx);

        ctx.Penalties.Should().NotBeEmpty();

        var present = ctx.Variables.GetSectionPresent(1) as BoolVar;
        ctx.Model.Add(present! == 1);
        ctx.Model.Minimize(LinearExpr.Sum(ctx.Penalties));

        var solver = new CpSolver();
        var status = solver.Solve(ctx.Model);

        status.Should().BeOneOf(new[] { CpSolverStatus.Optimal, CpSolverStatus.Feasible });
        solver.ObjectiveValue.Should().Be(5); // tek oda (Lecture) zorunlu → uyumsuz → H4Weight=5 penalty
    }
}
