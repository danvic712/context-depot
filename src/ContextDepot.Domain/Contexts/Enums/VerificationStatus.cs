using System.Text.Json.Serialization;

namespace ContextDepot.Domain.Contexts.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<VerificationStatus>))]
public enum VerificationStatus
{
    Unknown,
    Explicit,
    Inferred,
    Verified
}
