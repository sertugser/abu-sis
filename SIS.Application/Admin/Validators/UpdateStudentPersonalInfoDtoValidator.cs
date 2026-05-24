using FluentValidation;
using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Validators;

public class UpdateStudentPersonalInfoDtoValidator : AbstractValidator<UpdateStudentPersonalInfoDto>
{
    public UpdateStudentPersonalInfoDtoValidator()
    {
        // Contact — MaxLength mirrors the entity configuration.
        RuleFor(x => x.MobilePhone).MaximumLength(40);
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.SecondaryEmail).MaximumLength(120);
        RuleFor(x => x.AddressProvince).MaximumLength(120);
        RuleFor(x => x.AddressDistrict).MaximumLength(120);
        RuleFor(x => x.AddressLine).MaximumLength(250);
        RuleFor(x => x.PostalCode).MaximumLength(40);
        RuleFor(x => x.EmergencyContactName).MaximumLength(120);
        RuleFor(x => x.EmergencyContactPhone).MaximumLength(40);
        RuleFor(x => x.EmergencyContactRelation).MaximumLength(120);

        // Demographic
        RuleFor(x => x.BirthPlace).MaximumLength(120);
        RuleFor(x => x.Nationality).MaximumLength(120);
        RuleFor(x => x.BloodType).MaximumLength(10);
        RuleFor(x => x.PassportNo).MaximumLength(40);

        // Military
        RuleFor(x => x.MilitaryOffice).MaximumLength(120);

        // Photo
        RuleFor(x => x.PhotoExtension).MaximumLength(10);
    }
}
