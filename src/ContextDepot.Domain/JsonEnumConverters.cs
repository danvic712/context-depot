using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContextDepot.Domain;

public sealed class LowerCaseEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || !Enum.TryParse<TEnum>(reader.GetString(), true, out var value))
        {
            throw new JsonException($"Invalid {typeof(TEnum).Name} value.");
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        var text = value.ToString();
        writer.WriteStringValue(text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..]);
    }
}
