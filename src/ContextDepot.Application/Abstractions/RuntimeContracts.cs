namespace ContextDepot.Application.Abstractions;

public interface IIdGenerator
{
    Guid NewId();
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface ICurrentOwnerContext
{
    Guid OwnerId { get; }
}

public sealed class ContextDepotApplicationException(string errorCode, string message) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}
