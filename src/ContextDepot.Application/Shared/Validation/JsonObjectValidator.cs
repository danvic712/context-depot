using System.Text.Json;
using ContextDepot.Application.Shared.Exceptions;

namespace ContextDepot.Application.Shared.Validation;

public static class JsonObjectValidator
{
    public static void EnsureObject(string json, string errorCode)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ContextDepotApplicationException(errorCode);
            }
        }
        catch (JsonException)
        {
            throw new ContextDepotApplicationException(errorCode);
        }
    }
}
