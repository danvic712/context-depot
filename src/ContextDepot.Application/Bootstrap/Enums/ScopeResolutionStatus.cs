using System.Text.Json.Serialization;
using ContextDepot.Domain;

namespace ContextDepot.Application.Bootstrap.Enums;

[JsonConverter(typeof(LowerCaseEnumConverter<ScopeResolutionStatus>))]
public enum ScopeResolutionStatus
{
    [JsonStringEnumMemberName("resolved")]
    Resolved,
    [JsonStringEnumMemberName("ambiguous")]
    Ambiguous,
    [JsonStringEnumMemberName("broad")]
    Broad
}
