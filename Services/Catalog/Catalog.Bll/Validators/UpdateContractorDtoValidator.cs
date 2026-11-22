using Catalog.Bll.DTOs;
using FluentValidation;

namespace Catalog.Bll.Validators;

/// <summary>
/// FluentValidation validator for UpdateContractorDto.
/// Called explicitly in the PUT /api/contractors/{id} controller action.
/// </summary>
public sealed class UpdateContractorDtoValidator : AbstractValidator<UpdateContractorDto>
{
    public UpdateContractorDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Contractor name is required.")
            .MaximumLength(500).WithMessage("Name cannot exceed 500 characters.");

        RuleFor(x => x.AuthorizedCapital)
            .GreaterThan(0).WithMessage("Authorized capital must be greater than zero.");
    }
}
