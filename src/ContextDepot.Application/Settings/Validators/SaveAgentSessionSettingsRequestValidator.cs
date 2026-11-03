using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using FluentValidation;

namespace ContextDepot.Application.Settings.Validators;

public sealed class SaveAgentSessionSettingsRequestValidator : AbstractValidator<SaveAgentSessionSettingsRequest>
{
    public SaveAgentSessionSettingsRequestValidator()
    {
        RuleFor(request => request.Enabled).NotNull()
            .WithErrorCode(ApplicationErrorCodes.InvalidRequest)
            .WithMessage(ApplicationErrorCodes.InvalidRequest);
    }
}
