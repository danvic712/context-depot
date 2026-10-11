using System.Text.Json.Serialization;

namespace ContextDepot.Domain.Contexts.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<ProvenanceTrust>))]
public enum ProvenanceTrust
{
    [JsonStringEnumMemberName("unknown")]
    Unknown,
    [JsonStringEnumMemberName("asserted")]
    Asserted,
    [JsonStringEnumMemberName("attested")]
    Attested
}
