using ContextDepot.Domain.Exceptions;

namespace ContextDepot.Application.Shared.Exceptions;

public sealed class ContextDepotApplicationException : ContextDepotBusinessException
{
    public ContextDepotApplicationException(
        string errorCode, IReadOnlyDictionary<string, string[]>? fieldErrorCodes = null)
        : base(errorCode, fieldErrorCodes)
    {
        if (!ApplicationErrorMessages.Contains(errorCode))
        {
            throw new ArgumentOutOfRangeException(nameof(errorCode));
        }
    }
}
