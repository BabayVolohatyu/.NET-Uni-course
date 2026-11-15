using Catalog.Bll.DTOs;
using FluentValidation;

namespace Catalog.Bll.Validators;

/// <summary>
/// FluentValidation validator for CreateContractorDto.
/// Registered via AddValidatorsFromAssemblyContaining in Program.cs and
/// called EXPLICITLY in the controller action (not via deprecated AddFluentValidation middleware).
///
/// Provides richer messages than DataAnnotations — e.g. the EDRPOU regex rule with a
/// human-readable description of why it fails.
/// </summary>
public sealed class CreateContractorDtoValidator : AbstractValidator<CreateContractorDto>
{
    public CreateContractorDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Contractor name is required.")
            .MaximumLength(500).WithMessage("Name cannot exceed 500 characters.");

        RuleFor(x => x.EDRPOU)
            .NotEmpty()
            .Length(8).WithMessage("EDRPOU must be exactly 8 characters.")
            .Matches(@"^\d{8}$").WithMessage("EDRPOU must consist of exactly 8 numeric digits.");

        RuleFor(x => x.AuthorizedCapital)
            .GreaterThan(0).WithMessage("Authorized capital must be greater than zero.");

        RuleFor(x => x.RegistrationDate)
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Registration date cannot be in the future.")
            .GreaterThan(new DateTime(1991, 8, 24, 0, 0, 0, DateTimeKind.Utc))
                .WithMessage("Registration date cannot be before Ukraine's independence (24 Aug 1991).");

        // Optional nested detail — validate only when provided
        When(x => x.ContactEmail is not null, () =>
        {
            RuleFor(x => x.ContactEmail!)
                .EmailAddress().WithMessage("Contact email must be a valid email address.")
                .MaximumLength(320);
        });

        When(x => x.LegalAddress is not null, () =>
        {
            RuleFor(x => x.LegalAddress!)
                .MinimumLength(5).WithMessage("Legal address is too short.")
                .MaximumLength(1000);
        });
    }
}
