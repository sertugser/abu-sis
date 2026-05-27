namespace SIS.Application.AcademicProgression.DTOs;

public record PromotionCriterionDto(string Name, bool Passed, string? Detail);

public record StudentPromotionStatusDto(
    int CurrentLevel,
    int? EligibleTargetLevel,   // highest level the rules allow; null if cannot promote
    bool CanPromote,
    string? Reason,             // why not, if CanPromote == false
    int NormalDurationYears,
    IReadOnlyList<PromotionCriterionDto> Criteria);
