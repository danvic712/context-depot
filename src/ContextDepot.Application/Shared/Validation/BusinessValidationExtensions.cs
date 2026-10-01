using ContextDepot.Application.Shared.Exceptions;
using FluentValidation;

namespace ContextDepot.Application.Shared.Validation;

public static class BusinessValidationExtensions
{
    public static async Task ValidateBusinessRulesAsync<T>(
        this IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (result.IsValid) return;

        var fields = result.Errors.GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error =>
                ApplicationErrorMessages.Contains(error.ErrorCode)
                    ? error.ErrorCode
                    : ApplicationErrorCodes.InvalidRequest).Distinct().ToArray());
        throw new ContextDepotApplicationException(fields.First().Value[0], fields);
    }
}
