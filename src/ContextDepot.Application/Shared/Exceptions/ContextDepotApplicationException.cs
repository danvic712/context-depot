namespace ContextDepot.Application.Shared.Exceptions;

public sealed class ContextDepotApplicationException(string errorCode)
    : Exception(ApplicationErrorMessages.Get(errorCode))
{
    public string ErrorCode { get; } = errorCode;
}
