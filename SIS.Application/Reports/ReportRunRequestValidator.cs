using FluentValidation;

namespace SIS.Application.Reports;

/// <summary>
/// ReportRunRequest sınır doğrulaması (sayfa/sayfa boyutu + parametre sözlüğü).
/// Not: runner zaten Page/PageSize'ı clamp eder; bu validator konvansiyon + savunma katmanı.
/// </summary>
public sealed class ReportRunRequestValidator : AbstractValidator<ReportRunRequest>
{
    public ReportRunRequestValidator()
    {
        RuleFor(x => x.Parameters).NotNull();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 500);
        RuleFor(x => x.SortKey).MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.SortKey));
    }
}
