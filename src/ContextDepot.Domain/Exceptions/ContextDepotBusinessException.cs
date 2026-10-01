namespace ContextDepot.Domain.Exceptions;

public class ContextDepotBusinessException : Exception
{
    public ContextDepotBusinessException(
        string errorCode, IReadOnlyDictionary<string, string[]>? fieldErrorCodes = null) : base(errorCode)
    {
        if (string.IsNullOrEmpty(errorCode) || !char.IsAsciiLetter(errorCode[0]) ||
            errorCode.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_')))
        {
            throw new ArgumentException(DomainErrorCodes.InvalidErrorCode, nameof(errorCode));
        }

        ErrorCode = errorCode;
        FieldErrorCodes = fieldErrorCodes;
    }

    public string ErrorCode { get; }

    public IReadOnlyDictionary<string, string[]>? FieldErrorCodes { get; }
}
