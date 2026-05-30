using FluentValidation;
using SIS.Application.Attendance.DTOs;

namespace SIS.Application.Attendance.Validators;

public sealed class UpsertTermAttendancePolicyRequestValidator : AbstractValidator<UpsertTermAttendancePolicyRequest>
{
    private static readonly string[] AllowedMethods = ["Separate", "TotalHours", "Passive", "MarkOnly"];

    public UpsertTermAttendancePolicyRequestValidator()
    {
        RuleFor(x => x.TermWeekCount)
            .InclusiveBetween(1, 52)
            .WithMessage("Hafta sayısı 1-52 aralığında olmalı.");

        RuleFor(x => x.TheoryAbsenceLimitPercent)
            .InclusiveBetween(0m, 100m)
            .WithMessage("Teori limiti %0-100 aralığında olmalı.");

        RuleFor(x => x.LabAbsenceLimitPercent)
            .InclusiveBetween(0m, 100m)
            .WithMessage("Uygulama limiti %0-100 aralığında olmalı.");

        RuleFor(x => x.Method)
            .Must(m => m != null && AllowedMethods.Contains(m.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Geçerli yöntem: Separate, TotalHours, Passive, MarkOnly.");
    }
}
