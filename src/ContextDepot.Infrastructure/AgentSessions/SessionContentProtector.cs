using ContextDepot.Application.AgentSessions.Contracts;
using Microsoft.AspNetCore.DataProtection;

namespace ContextDepot.Infrastructure.AgentSessions;

public sealed class SessionContentProtector(IDataProtectionProvider provider) : ISessionContentProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("ContextDepot.SessionCapture.Content.v1");
    public string Protect(string content) => protector.Protect(content);
    public string Unprotect(string content) => protector.Unprotect(content);
}
