using System.Text.Json.Serialization;

namespace ContextDepot.Application.Settings.Enums;

public enum Language
{
    [JsonStringEnumMemberName("zh-CN")]
    ZhCn,
    [JsonStringEnumMemberName("en-US")]
    EnUs
}
