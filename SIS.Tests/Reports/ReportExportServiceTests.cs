using FluentAssertions;
using SIS.Application.Reports;
using SIS.Infrastructure.Services.Reports;

namespace SIS.Tests.Reports;

public class ReportExportServiceTests
{
    private static ReportResult Sample() => new(
        new[] { new ReportColumn("no", "No"), new ReportColumn("ad", "Ad", ReportColumnAlign.Right) },
        new List<IReadOnlyList<object?>>
        {
            new object?[] { "2021001", "Ali" },
            new object?[] { "2021002", "Veli" },
        },
        2);

    [Fact]
    public void ToXlsx_ProducesNonEmptyZipFile()
    {
        var svc = new ReportExportService();
        var bytes = svc.ToXlsx("Öğrenci Listesi", Sample());
        bytes.Should().NotBeNullOrEmpty();
        // xlsx = ZIP: ilk iki bayt "PK"
        bytes[0].Should().Be((byte)'P');
        bytes[1].Should().Be((byte)'K');
    }

    [Fact]
    public void ToPdf_ProducesNonEmptyPdfFile()
    {
        var svc = new ReportExportService();
        var bytes = svc.ToPdf("Öğrenci Listesi", Sample());
        bytes.Should().NotBeNullOrEmpty();
        // pdf: "%PDF"
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    // Not Durum Çizelgesi gibi raporlar null Score/MakeupScore üretebilir.
    private static ReportResult WithNullCells() => new(
        new[] { new ReportColumn("no", "No"), new ReportColumn("puan", "Puan", ReportColumnAlign.Right) },
        new List<IReadOnlyList<object?>>
        {
            new object?[] { "2021001", 75m },
            new object?[] { "2021002", null },   // puan girilmemiş
        },
        2);

    [Fact]
    public void ToXlsx_HandlesNullCells()
    {
        var svc = new ReportExportService();
        var act = () => svc.ToXlsx("Not Durum", WithNullCells());
        act.Should().NotThrow();
    }

    [Fact]
    public void ToPdf_HandlesNullCells()
    {
        var svc = new ReportExportService();
        var act = () => svc.ToPdf("Not Durum", WithNullCells());
        act.Should().NotThrow();
    }
}
