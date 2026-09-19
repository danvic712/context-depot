using System.Text.Json.Serialization;
using ContextDepot.Domain.Shared;

namespace ContextDepot.Domain.Contexts.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<VerificationStatus>))]
public enum VerificationStatus
{
    Unknown,
    Explicit,
    Inferred,
    Verified
}
