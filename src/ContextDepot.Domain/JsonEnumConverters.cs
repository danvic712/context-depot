using ContextDepot.Domain.Exceptions;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContextDepot.Domain;

public sealed class LowerCaseEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (text is null ||
            !Enum.TryParse<TEnum>(text, true, out var value) ||
            !string.Equals(Enum.GetName(value), text, StringComparison.OrdinalIgnoreCase))
        {
            throw new JsonException(DomainErrorCodes.InvalidEnumValue);
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        var text = Enum.GetName(value);
        if (text is null)
        {
            throw new JsonException(DomainErrorCodes.InvalidEnumValue);
        }

        writer.WriteStringValue(char.ToLowerInvariant(text[0]) + text[1..]);
    }
}
