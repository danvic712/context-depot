using System.Text.Json.Serialization;

namespace ContextDepot.Application.Settings.Enums;

public enum Theme
{
    [JsonStringEnumMemberName("system")]
    System,
    [JsonStringEnumMemberName("light")]
    Light,
    [JsonStringEnumMemberName("dark")]
    Dark
}
