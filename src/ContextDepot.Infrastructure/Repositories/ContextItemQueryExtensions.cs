using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Infrastructure.Repositories;

internal static class ContextItemQueryExtensions
{
    public static IQueryable<ContextItem> WhereRetrievableAt(
        this IQueryable<ContextItem> query,
        DateTimeOffset now) =>
        query.Where(context =>
            context.Status == ContextStatus.Active &&
            (context.ValidFrom == null || context.ValidFrom <= now) &&
            (context.ValidUntil == null || context.ValidUntil > now) &&
            (context.ExpiresAt == null || context.ExpiresAt > now));
}
