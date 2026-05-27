namespace SIS.Application.Reports;

/// <summary>
/// Bir rapor türü. Code = ReportDefinition.Code ile eşleşir (stable slug).
/// Yeni rapor eklemek = bu arayüzü implemente eden yeni bir sınıf yazmak.
/// </summary>
public interface IReportHandler
{
    /// <summary>ReportDefinition.Code ile birebir eşleşen stable slug.</summary>
    string Code { get; }

    /// <summary>Filtre ekranının çizileceği parametre şeması.</summary>
    ReportParameterSchema GetParameterSchema();

    /// <summary>Veriyi üretir. Scope filtresini ctx üzerinden uygular.</summary>
    Task<ReportResult> RunAsync(ReportRunRequest request, ReportExecutionContext ctx, CancellationToken ct);
}
