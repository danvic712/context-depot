using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Exceptions;

namespace ContextDepot.Application.Documents;

public sealed class DocumentLinkValidationException(IReadOnlyList<DocumentLinkDiagnostic> errors)
    : ContextDepotBusinessException(ApplicationErrorCodes.InvalidDocumentLinks)
{
    public IReadOnlyList<DocumentLinkDiagnostic> LinkErrors { get; } = errors;
}
