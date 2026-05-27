using FluentValidation;
using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Validators;

public class UpsertStudentPlacementDtoValidator : AbstractValidator<UpsertStudentPlacementDto>
{
    public UpsertStudentPlacementDtoValidator()
    {
        // EntranceType is optional; when present it must be a valid enum name.
        RuleFor(x => x.EntranceType)
            .Must(t => string.IsNullOrWhiteSpace(t)
                || Enum.TryParse<SIS.Domain.Enums.StudentEntranceType>(t, true, out _))
            .WithMessage("Geçersiz giriş türü.");

        // Placement year stays within a sensible range when provided.
        RuleFor(x => x.PlacementYear)
            .InclusiveBetween(1980, DateTime.UtcNow.Year + 1)
            .When(x => x.PlacementYear.HasValue);

        // Free-text lengths mirror the entity configuration.
        RuleFor(x => x.ScoreType).MaximumLength(20);
        RuleFor(x => x.QuotaType).MaximumLength(80);
        RuleFor(x => x.PlacedProgramName).MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
