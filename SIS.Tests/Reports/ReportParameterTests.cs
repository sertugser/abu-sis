using FluentAssertions;
using SIS.Application.Reports;

namespace SIS.Tests.Reports;

public class ReportParameterTests
{
    [Fact]
    public void Schema_Required_DefaultsFalse_AndStoresOptions()
    {
        var p = new ReportParameter("status", "Durum", ReportParameterType.Enum, Required: false,
            Options: new[] { new ReportParameterOption("Active", "Aktif") });

        p.Required.Should().BeFalse();
        p.Type.Should().Be(ReportParameterType.Enum);
        p.Options.Should().ContainSingle(o => o.Value == "Active" && o.Label == "Aktif");
    }
}
