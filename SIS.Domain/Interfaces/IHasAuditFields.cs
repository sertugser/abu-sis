namespace SIS.Domain.Interfaces;

/// <summary>
/// Audit alanlarına sahip entity'lerin implement ettiği interface.
/// SaveChangesInterceptor bu interface üzerinden CreatedAt/UpdatedAt'i otomatik set eder.
/// </summary>
public interface IHasAuditFields
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}
