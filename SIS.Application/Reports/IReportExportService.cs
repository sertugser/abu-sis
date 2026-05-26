namespace SIS.Application.Reports;

public interface IReportExportService
{
    byte[] ToXlsx(string title, ReportResult result);
    byte[] ToPdf(string title, ReportResult result);
}
