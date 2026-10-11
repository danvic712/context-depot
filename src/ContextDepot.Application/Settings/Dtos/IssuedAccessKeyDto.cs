namespace ContextDepot.Application.Settings.Dtos;

public sealed record IssuedAccessKeyDto(AccessKeyDto Key, string Secret)
{
    public override string ToString() => $"{nameof(IssuedAccessKeyDto)} {{ Secret = [REDACTED] }}";
}
