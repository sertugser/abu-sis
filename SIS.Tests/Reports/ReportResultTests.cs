using FluentAssertions;
using SIS.Application.Reports;

namespace SIS.Tests.Reports;

public class ReportResultTests
{
    [Fact]
    public void ReportResult_HoldsColumnsRowsAndTotal()
    {
        var result = new ReportResult(
            Columns: new[] { new ReportColumn("no", "No", ReportColumnAlign.Left) },
            Rows: new List<IReadOnlyList<object?>> { new object?[] { "2021001" } },
            TotalCount: 1);

        result.Columns.Should().ContainSingle(c => c.Key == "no");
        result.Rows.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
    }
}
