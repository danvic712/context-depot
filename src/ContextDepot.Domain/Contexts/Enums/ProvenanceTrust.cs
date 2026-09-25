using System.Text.Json.Serialization;

namespace ContextDepot.Domain.Contexts.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<ProvenanceTrust>))]
public enum ProvenanceTrust
{
    Unknown,
    Asserted,
    Attested
}
