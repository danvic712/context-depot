using System.Text.Json.Serialization;

namespace ContextDepot.Domain.Contexts.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<VerificationStatus>))]
public enum VerificationStatus
{
    [JsonStringEnumMemberName("unknown")]
    Unknown,
    [JsonStringEnumMemberName("explicit")]
    Explicit,
    [JsonStringEnumMemberName("inferred")]
    Inferred,
    [JsonStringEnumMemberName("verified")]
    Verified
}
