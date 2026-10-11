using System.Text;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Workspaces.Dtos;

namespace ContextDepot.Application.Workspaces;

public static class WorkspaceSubjectNormalizer
{
    public static WorkspaceSubject Normalize(WorkspaceSubject subject, ISourceSafetyService sourceSafety)
    {
        if (subject is null || !Enum.IsDefined(subject.Type) || string.IsNullOrWhiteSpace(subject.Key))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidWorkspaceSubject);
        string key;
        try { key = subject.Key.Trim().Normalize(NormalizationForm.FormC); }
        catch (ArgumentException) { throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidWorkspaceSubject); }
        if (key.Length > 200 || key.Any(char.IsControl))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidWorkspaceSubject);
        sourceSafety.EnsureSafe(key);
        return subject with { Key = key };
    }
}
