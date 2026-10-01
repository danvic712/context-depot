using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using FluentValidation;

namespace ContextDepot.Application.Settings.Validators;

public sealed class LanguageRequestValidator : AbstractValidator<LanguageRequest>
{
    public LanguageRequestValidator()
    {
        RuleFor(request => request.Language)
            .NotNull().WithErrorCode(ApplicationErrorCodes.InvalidAppearanceLanguage)
            .IsInEnum().WithErrorCode(ApplicationErrorCodes.InvalidAppearanceLanguage);
    }
}
