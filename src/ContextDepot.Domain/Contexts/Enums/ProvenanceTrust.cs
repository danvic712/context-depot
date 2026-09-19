using System.Text.Json.Serialization;
using ContextDepot.Domain.Shared;

namespace ContextDepot.Domain.Contexts.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<ProvenanceTrust>))]
public enum ProvenanceTrust
{
    Unknown,
    Asserted,
    Attested
}
