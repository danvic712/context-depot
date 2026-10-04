using ContextDepot.Domain.Exceptions;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContextDepot.Domain;

public sealed class LowerCaseEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    private static readonly JsonConverter<TEnum> NativeConverter =
        (JsonConverter<TEnum>)new JsonStringEnumConverter<TEnum>(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
            .CreateConverter(typeof(TEnum), JsonSerializerOptions.Default);

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (text is null)
        {
            throw new JsonException(DomainErrorCodes.InvalidEnumValue);
        }

        TEnum value;
        try
        {
            value = NativeConverter.Read(ref reader, typeToConvert, options);
        }
        catch (JsonException)
        {
            throw new JsonException(DomainErrorCodes.InvalidEnumValue);
        }

        // The public contract accepts one exact name, without whitespace or combinations.
        if (!string.Equals(Enum.GetName(value), text, StringComparison.OrdinalIgnoreCase))
        {
            throw new JsonException(DomainErrorCodes.InvalidEnumValue);
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        try
        {
            NativeConverter.Write(writer, value, options);
        }
        catch (JsonException)
        {
            throw new JsonException(DomainErrorCodes.InvalidEnumValue);
        }
    }
}
