using ContextDepot.Application.Settings.Dtos;

namespace ContextDepot.Application.Setup.Dtos;

public sealed record SetupCompletionDto(SetupStatusDto Status, IssuedAccessKeyDto? AccessKey = null)
{
    public override string ToString() => $"{nameof(SetupCompletionDto)} {{ Status = {Status}, AccessKey = [REDACTED] }}";
}
