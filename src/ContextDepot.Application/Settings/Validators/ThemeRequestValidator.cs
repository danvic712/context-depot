using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using FluentValidation;

namespace ContextDepot.Application.Settings.Validators;

public sealed class ThemeRequestValidator : AbstractValidator<ThemeRequest>
{
    public ThemeRequestValidator()
    {
        RuleFor(request => request.Theme)
            .NotNull().WithErrorCode(ApplicationErrorCodes.InvalidAppearanceTheme).WithMessage(ApplicationErrorCodes.InvalidAppearanceTheme)
            .IsInEnum().WithErrorCode(ApplicationErrorCodes.InvalidAppearanceTheme).WithMessage(ApplicationErrorCodes.InvalidAppearanceTheme);
    }
}
