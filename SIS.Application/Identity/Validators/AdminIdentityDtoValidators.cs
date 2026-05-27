using FluentValidation;
using SIS.Application.Identity.DTOs;

namespace SIS.Application.Identity.Validators;

public class UpdateMenuModuleDtoValidator : AbstractValidator<UpdateMenuModuleDto>
{
    public UpdateMenuModuleDtoValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Icon).MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.Icon));
        RuleFor(x => x.AllowedRolesCsv).MaximumLength(300).When(x => !string.IsNullOrWhiteSpace(x.AllowedRolesCsv));
    }
}

public class UpdateMenuItemDtoValidator : AbstractValidator<UpdateMenuItemDto>
{
    public UpdateMenuItemDtoValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Route)
            .NotEmpty()
            .MaximumLength(300)
            .Must(route => route.StartsWith('/'))
            .WithMessage("Route '/' ile başlamalıdır.");
        RuleFor(x => x.PermissionKeys).NotNull();
        RuleForEach(x => x.PermissionKeys).NotEmpty().MaximumLength(200);
    }
}

public class ReorderModulesDtoValidator : AbstractValidator<ReorderModulesDto>
{
    public ReorderModulesDtoValidator()
    {
        RuleFor(x => x.ParentId).GreaterThan(0).When(x => x.ParentId.HasValue);
        RuleFor(x => x.Entries).NotNull();
        RuleForEach(x => x.Entries).SetValidator(new ReorderEntryValidator());
    }
}

public class ReorderItemsDtoValidator : AbstractValidator<ReorderItemsDto>
{
    public ReorderItemsDtoValidator()
    {
        RuleFor(x => x.ModuleId).GreaterThan(0);
        RuleFor(x => x.Entries).NotNull();
        RuleForEach(x => x.Entries).SetValidator(new ReorderEntryValidator());
    }
}

public class ReorderEntryValidator : AbstractValidator<ReorderEntry>
{
    public ReorderEntryValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.SortOrder).GreaterThan(0);
    }
}

public class CreatePermissionBundleDtoValidator : AbstractValidator<CreatePermissionBundleDto>
{
    public CreatePermissionBundleDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.Category).MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.Category));
        RuleFor(x => x.PermissionIds).NotNull();
        RuleForEach(x => x.PermissionIds).GreaterThan(0);
    }
}

public class UpdatePermissionBundleDtoValidator : AbstractValidator<UpdatePermissionBundleDto>
{
    public UpdatePermissionBundleDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.Category).MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.Category));
        RuleFor(x => x.PermissionIds).NotNull();
        RuleForEach(x => x.PermissionIds).GreaterThan(0);
    }
}

public class CreateReportCategoryDtoValidator : AbstractValidator<CreateReportCategoryDto>
{
    public CreateReportCategoryDtoValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100).Matches("^[A-Za-z0-9_.-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class UpdateReportCategoryDtoValidator : AbstractValidator<UpdateReportCategoryDto>
{
    public UpdateReportCategoryDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class CreateReportDefinitionDtoValidator : AbstractValidator<CreateReportDefinitionDto>
{
    public CreateReportDefinitionDtoValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100).Matches("^[A-Za-z0-9_.-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.CategoryId).GreaterThan(0);
    }
}

public class UpdateReportDefinitionDtoValidator : AbstractValidator<UpdateReportDefinitionDto>
{
    public UpdateReportDefinitionDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.CategoryId).GreaterThan(0);
    }
}

public class CreateReportUserGroupDtoValidator : AbstractValidator<CreateReportUserGroupDto>
{
    public CreateReportUserGroupDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}

public class UpdateReportUserGroupDtoValidator : AbstractValidator<UpdateReportUserGroupDto>
{
    public UpdateReportUserGroupDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}

public class AddGroupMemberDtoValidator : AbstractValidator<AddGroupMemberDto>
{
    public AddGroupMemberDtoValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
    }
}

public class SetReportAccessGrantDtoValidator : AbstractValidator<SetReportAccessGrantDto>
{
    public SetReportAccessGrantDtoValidator()
    {
        RuleFor(x => x.ScopeKind).InclusiveBetween(0, 1);
        RuleFor(x => x.SubjectKind).InclusiveBetween(0, 2);
        RuleFor(x => x.Effect).InclusiveBetween(0, 1);
        RuleFor(x => x.TargetId).GreaterThan(0);
        RuleFor(x => x.SubjectId).GreaterThan(0);
        RuleFor(x => x.Reason).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.Reason));
    }
}

public class SetUserScopeDtoValidator : AbstractValidator<SetUserScopeDto>
{
    public SetUserScopeDtoValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.FacultyId).GreaterThan(0).When(x => x.FacultyId.HasValue);
    }
}

public class RevokeDelegationDtoValidator : AbstractValidator<RevokeDelegationDto>
{
    public RevokeDelegationDtoValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.Reason));
    }
}
