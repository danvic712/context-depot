namespace ContextDepot.Application.AgentSessions.Contracts;

public interface ISessionContentProtector
{
    string Protect(string content);

    string Unprotect(string content);
}
