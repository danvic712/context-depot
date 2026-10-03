using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ContextDepot.Infrastructure.Tests;

// Keep production SQL and migrations in this test schema.
internal sealed class InferenceTestSchemaInterceptor(string schema) : DbCommandInterceptor
{
    private void Rewrite(DbCommand command) => command.CommandText = command.CommandText
        .Replace("\"public\"", $"\"{schema}\"", StringComparison.Ordinal)
        .Replace("'public'", $"'{schema}'", StringComparison.Ordinal)
        .Replace("public.", schema + ".", StringComparison.Ordinal);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { Rewrite(command); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Rewrite(command); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    { Rewrite(command); return ValueTask.FromResult(result); }
}
