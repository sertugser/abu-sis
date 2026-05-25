using FluentValidation;
using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Validators;

public class UpsertScholarshipDetailDtoValidator : AbstractValidator<UpsertScholarshipDetailDto>
{
    public UpsertScholarshipDetailDtoValidator()
    {
        // Type must be a valid ScholarshipType enum name (case-insensitive).
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(t => Enum.TryParse<SIS.Domain.Enums.ScholarshipType>(t, true, out _))
            .WithMessage("Geçersiz burs türü.");

        // Rate is a percentage when provided.
        RuleFor(x => x.RatePercent)
            .InclusiveBetween(0, 100)
            .When(x => x.RatePercent.HasValue);

        // Free-text lengths mirror the entity configuration.
        RuleFor(x => x.Source).MaximumLength(160);
        RuleFor(x => x.Note).MaximumLength(500);

        // Cross-field: end date cannot precede start date when both are set.
        RuleFor(x => x)
            .Must(x => !(x.StartDate.HasValue && x.EndDate.HasValue) || x.EndDate >= x.StartDate)
            .WithMessage("Bitiş tarihi başlangıçtan önce olamaz.");
    }
}
