namespace ContextDepot.Infrastructure.Configuration;

public sealed class ApplicationSettingRecord
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string ValueJson { get; set; } = "\"\"";

    public DateTimeOffset UpdatedAt { get; set; }
}
