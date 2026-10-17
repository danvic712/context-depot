namespace ContextDepot.Infrastructure.Tests;

public sealed class TemporaryPostgreSqlTheoryAttribute : TheoryAttribute
{
    public TemporaryPostgreSqlTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CONTEXTDEPOT_TEMP_TEST_CONNECTION")))
            Skip = "Set CONTEXTDEPOT_TEMP_TEST_CONNECTION to verify using connection-local temporary tables only.";
    }
}
