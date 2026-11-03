using Hangfire;
using Hangfire.Common;

namespace ContextDepot.Infrastructure.Jobs;

public sealed class BackgroundJobRegistrar(IRecurringJobManager recurringJobs)
{
    public void Register()
    {
        var options = new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Utc,
            MisfireHandling = MisfireHandlingMode.Ignorable
        };

        recurringJobs.AddOrUpdate("cd:session-capture",
            Job.FromExpression<SessionCaptureJob>(job => job.ExecuteAsync(CancellationToken.None)), "* * * * *", options);
        recurringJobs.AddOrUpdate("cd:index-repair",
            Job.FromExpression<IndexRepairJob>(job => job.ExecuteAsync(CancellationToken.None)), "* * * * *", options);
        recurringJobs.AddOrUpdate("cd:settings-refresh",
            Job.FromExpression<ApplicationSettingsRefreshJob>(job => job.ExecuteAsync(CancellationToken.None)), "* * * * *", options);
        recurringJobs.AddOrUpdate("cd:inference-refresh",
            Job.FromExpression<InferenceRuntimeRefreshJob>(job => job.ExecuteAsync(CancellationToken.None)), "* * * * *", options);
    }
}
