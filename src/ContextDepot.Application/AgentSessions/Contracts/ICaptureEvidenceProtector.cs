namespace ContextDepot.Application.AgentSessions.Contracts;
public interface ICaptureEvidenceProtector
{
    string Protect(string content);
    string Unprotect(string content);
}
