namespace ContextDepot.Domain.Settings;

public sealed class ApplicationSetting
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string ValueJson { get; set; } = "\"\"";

    public DateTimeOffset UpdatedAt { get; set; }
}
