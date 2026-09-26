namespace ContextDepot.Application.Shared.Exceptions;

public sealed class ContextDepotApplicationException(string errorCode)
    : Exception(errorCode)
{
    public string ErrorCode { get; } = errorCode;
}
