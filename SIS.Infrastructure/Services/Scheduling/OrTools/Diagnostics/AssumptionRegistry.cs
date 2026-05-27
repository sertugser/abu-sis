using Google.OrTools.Sat;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;

public sealed record AssumptionMetadata(
    string ConstraintCode,
    int? PrimarySectionId,
    int? SecondarySectionId);

/// <summary>
/// Hard constraint instance'ları için BoolVar (assumption literal) registry'si.
/// CP-SAT INFEASIBLE durumunda solver.SufficientAssumptionsForInfeasibility() bu literal'lerin
/// minimal alt kümesini döner — UnsatCoreInterpreter buradan human-readable sebep çıkarır.
/// </summary>
public sealed class AssumptionRegistry
{
    private readonly CpModel _model;
    private readonly Dictionary<ILiteral, AssumptionMetadata> _byLiteral = new();
    // Literal'in CpModel proto indeksi → metadata. solver.SufficientAssumptionsForInfeasibility()
    // assumption literal'lerinin PROTO indekslerini döner (dizi pozisyonlarını DEĞİL); doğru çözüm
    // için bu index→metadata haritası gerekir.
    private readonly Dictionary<int, AssumptionMetadata> _byIndex = new();
    private readonly List<ILiteral> _ordered = new();

    public AssumptionRegistry(CpModel model)
    {
        _model = model;
    }

    public int Count => _ordered.Count;

    public ILiteral Register(string constraintCode,
        int? primarySectionId = null,
        int? secondarySectionId = null)
    {
        var name = $"{constraintCode}_s{primarySectionId}_s{secondarySectionId}";
        var literal = _model.NewBoolVar(name);
        var metadata = new AssumptionMetadata(constraintCode, primarySectionId, secondarySectionId);
        _byLiteral[literal] = metadata;
        _byIndex[literal.GetIndex()] = metadata;
        _ordered.Add(literal);
        return literal;
    }

    public void RegisterAllAsAssumptions()
    {
        if (_ordered.Count > 0)
            _model.AddAssumptions(_ordered.ToArray());
    }

    public AssumptionMetadata? LookupByLiteral(ILiteral literal) =>
        _byLiteral.TryGetValue(literal, out var meta) ? meta : null;

    /// <summary>
    /// solver.SufficientAssumptionsForInfeasibility()'in döndürdüğü literal/proto indeksinden
    /// metadata çözer. UnsatCoreInterpreter bunu kullanır (eski "dizi pozisyonu" varsayımı yanlıştı).
    /// </summary>
    public AssumptionMetadata? LookupByIndex(int literalIndex) =>
        _byIndex.TryGetValue(literalIndex, out var meta) ? meta : null;

    public IReadOnlyList<ILiteral> AllLiterals => _ordered;
}
