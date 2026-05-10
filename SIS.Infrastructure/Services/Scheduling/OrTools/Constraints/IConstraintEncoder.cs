namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;

public interface IConstraintEncoder
{
    /// <summary>Constraint code (örn. "H1", "S2"). DB'deki ConstraintDefinition.Code ile eşleşir.</summary>
    string Code { get; }

    /// <summary>true = hard constraint, false = soft (penalty).</summary>
    bool IsHard { get; }

    /// <summary>Model'e bu constraint'i ekle. IsEnabled flag'i ctx.ConstraintFlags üzerinden kontrol edilir.</summary>
    void Encode(EncoderContext ctx);
}
