using FluentAssertions;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;

namespace SIS.Tests.Scheduling.OrTools.Diagnostics;

public class UnsatCoreInterpreterTests
{
    private static SectionInput Section(int id, int sectionNumber, string courseCode = "CENG101") => new()
    {
        Id = id,
        CourseId = 100,
        CourseCode = courseCode,
        CourseName = "X",
        SectionNumber = sectionNumber,
        Capacity = 40,
        TheoryHours = 3,
        LabHours = 0,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    [Fact]
    public void BuildReason_H8_TwoSectionsKnown_ProducesPairReason()
    {
        var interpreter = new UnsatCoreInterpreter();
        var sectionLookup = new Dictionary<int, SectionInput>
        {
            { 1, Section(1, sectionNumber: 1) },
            { 2, Section(2, sectionNumber: 2) }
        };
        var meta = new AssumptionMetadata("H8", PrimarySectionId: 1, SecondarySectionId: 2);

        var reason = interpreter.BuildReason(meta, sectionLookup);

        reason.Should().Contain("Aynı dersin");
        reason.Should().Contain("CENG101");
        reason.Should().Contain("§1");
        reason.Should().Contain("§2");
    }

    [Fact]
    public void BuildReason_H1_OneClassroomKnown_ProducesClassroomReason()
    {
        // H1 metadata uses primarySectionId for classroomId, secondarySectionId is null
        var interpreter = new UnsatCoreInterpreter();
        var sectionLookup = new Dictionary<int, SectionInput>();  // empty — primary won't match
        var meta = new AssumptionMetadata("H1", PrimarySectionId: 9, SecondarySectionId: null);

        var reason = interpreter.BuildReason(meta, sectionLookup);

        reason.Should().Contain("Derslik çakışması");
        // Falls back to numeric ID since lookup fails
        reason.Should().Contain("9");
    }

    [Fact]
    public void BuildReason_UnknownConstraintCode_ReturnsCodeWithFallback()
    {
        var interpreter = new UnsatCoreInterpreter();
        var meta = new AssumptionMetadata("H99", PrimarySectionId: 1, SecondarySectionId: 2);

        var reason = interpreter.BuildReason(meta, new Dictionary<int, SectionInput>());

        // Unknown constraint code → falls back to using the code itself as a label
        reason.Should().Contain("H99");
    }

    [Fact]
    public void BuildReason_H6_OneSectionKnown_ProducesSingleSectionReason()
    {
        var interpreter = new UnsatCoreInterpreter();
        var sectionLookup = new Dictionary<int, SectionInput>
        {
            { 1, Section(1, sectionNumber: 1) }
        };
        var meta = new AssumptionMetadata("H6", PrimarySectionId: 1, SecondarySectionId: null);

        var reason = interpreter.BuildReason(meta, sectionLookup);

        reason.Should().Contain("teori-lab");
        reason.Should().Contain("§1");
        reason.Should().Contain("CENG101");
    }

    [Fact]
    public void BuildReason_NullPrimaryAndSecondary_ProducesGenericConstraintName()
    {
        var interpreter = new UnsatCoreInterpreter();
        var meta = new AssumptionMetadata("H10", PrimarySectionId: null, SecondarySectionId: null);

        var reason = interpreter.BuildReason(meta, new Dictionary<int, SectionInput>());

        reason.Should().Contain("Fakülte derslik kısıtlaması");
    }
}
