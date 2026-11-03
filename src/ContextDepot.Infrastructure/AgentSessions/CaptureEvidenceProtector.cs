using ContextDepot.Application.AgentSessions.Contracts;
using Microsoft.AspNetCore.DataProtection;

namespace ContextDepot.Infrastructure.AgentSessions;

public sealed class CaptureEvidenceProtector(IDataProtectionProvider provider) : ICaptureEvidenceProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("ContextDepot.SessionCapture.Evidence.v1");
    public string Protect(string content) => protector.Protect(content);
    public string Unprotect(string content) => protector.Unprotect(content);
}
