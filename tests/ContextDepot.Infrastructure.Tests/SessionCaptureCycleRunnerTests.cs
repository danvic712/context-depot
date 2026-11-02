using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.AgentSessions.Enums;
using ContextDepot.Domain.AgentSessions;
using ContextDepot.Application.Shared.Safety;
using ContextDepot.Infrastructure.AgentSessions;
using ContextDepot.Infrastructure.CurrentDepot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Tests;

public sealed class SessionCaptureCycleRunnerTests
{
    [Fact]
    public async Task Live_switch_keeps_cleanup_running_but_only_claims_when_enabled()
    {
        var options = new MutableMonitor(); var queue = new Queue(); var inbox = new EmptyInbox();
        using var provider = Provider(queue, inbox);
        var runner = Runner(provider, options);
        await runner.RunAsync(default);
        Assert.Equal(["cleanup"], queue.Calls);
        options.CurrentValue = new() { Enabled = true };
        await runner.RunAsync(default);
        Assert.Equal(["cleanup", "cleanup", "claim"], queue.Calls);
        options.CurrentValue = new() { Enabled = false };
        await runner.RunAsync(default);
        Assert.Equal(["cleanup", "cleanup", "claim", "cleanup"], queue.Calls);
        options.CurrentValue = new() { Enabled = true };
        await runner.RunAsync(default);
        Assert.Equal(["cleanup", "cleanup", "claim", "cleanup", "cleanup", "claim"], queue.Calls);
        Assert.Equal(4, inbox.Cleanups);
    }

    [Fact]
    public async Task Disabling_after_claim_allows_claimed_work_to_finish_without_authorization()
    {
        var options = new MutableMonitor { CurrentValue = new() { Enabled = true } };
        var turn = Turn("user");
        var queue = new Queue { Turn = turn, OnClaim = () => options.CurrentValue = new() { Enabled = false } };
        using var provider = Provider(queue, new EmptyInbox(), () => new MemoryCaptureProcessor(new NoAccess(), null!, null!, null!, null!, null!));
        await Runner(provider, options).RunAsync(default);
        Assert.False(options.CurrentValue.Enabled);
        Assert.Equal(["cleanup", "claim", "finish:skipped_unauthorized:Completed"], queue.Calls);
    }

    [Fact]
    public async Task Disabling_before_plan_commit_rejects_inflight_model_result()
    {
        const string quote = "项目的持久化数据保存在 PostgreSQL 数据库中。";
        var options = new MutableMonitor { CurrentValue = new() { Enabled = true } };
        var queue = new Queue { Turn = Turn(quote) };
        var extraction = new DelayedExtraction(); var plans = new SwitchGuardedPlans(options);
        using var provider = Provider(queue, new EmptyInbox(), () => new MemoryCaptureProcessor(new Authorized(), extraction,
            null!, new HighConfidenceSecretDetector(), plans, new GrantedBudget()));
        var runner = Runner(provider, options);
        var running = runner.RunAsync(default);
        await extraction.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        options.CurrentValue = new() { Enabled = false };
        extraction.Result.SetResult([new MemoryCandidate("fact", "project.database", quote, quote, 0.95m)]);
        await running.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, plans.StoreAttempts);
        Assert.Equal(["cleanup", "claim", "finish::Failed"], queue.Calls);
        await runner.RunAsync(default);
        Assert.Equal("cleanup", queue.Calls[^1]);
    }

    private static CapturedTurn Turn(string quote) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        DateTimeOffset.UtcNow.AddMinutes(1), quote, "好的。", LeaseToken: Guid.NewGuid());
    private static ServiceProvider Provider(Queue queue, EmptyInbox inbox, Func<MemoryCaptureProcessor>? processor = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<CurrentDepotAccessContext>();
        services.AddSingleton<ISessionCaptureRepository>(queue);
        services.AddSingleton<ISessionCaptureInboxRepository>(inbox);
        if (processor is not null) services.AddScoped(_ => processor());
        return services.BuildServiceProvider();
    }
    private static SessionCaptureCycleRunner Runner(ServiceProvider provider, MutableMonitor options) =>
        new(provider.GetRequiredService<IServiceScopeFactory>(), options, NullLogger<SessionCaptureCycleRunner>.Instance);

    private sealed class SwitchGuardedPlans(MutableMonitor options) : ISessionCapturePlanRepository
    {
        public int StoreAttempts { get; private set; }
        public Task<LeaseCommitResult> StorePlanAsync(CapturedTurn turn, ValidatedExtractionPlan plan, CancellationToken cancellationToken)
        {
            StoreAttempts++;
            Assert.False(options.CurrentValue.Enabled);
            return Task.FromResult(new LeaseCommitResult("lease_lost"));
        }
        public Task<CaptureCandidateOutcome> SaveCandidateAsync(CapturedTurn turn, int index, PreparedCaptureContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LeaseCommitResult> RejectCandidateAsync(CapturedTurn turn, int index, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class GrantedBudget : ISessionCaptureCallBudget
    {
        public Task<BudgetReservation> TryReserveAsync(CaptureWorkLease lease, ProviderCallKind kind, CancellationToken cancellationToken) => Task.FromResult(new BudgetReservation("granted"));
    }
    private sealed class EmptyInbox : ISessionCaptureInboxRepository
    {
        public int Cleanups { get; private set; }
        public Task<SessionCaptureReceipt> AcceptAsync(AuthorizedCaptureEnvelope envelope, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<RouteLocator>> FindRoutingCandidatesAsync(int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RouteLocator>>([]);
        public Task<RouteLease?> TryClaimRouteAsync(RouteLocator locator, CancellationToken cancellationToken) => Task.FromResult<RouteLease?>(null);
        public Task<bool> StoreRoutingVectorAsync(RouteLease lease, string vector, string hash, string fingerprint, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> CompleteRouteAsync(RouteLease lease, RouteDecision decision, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> PromoteAsync(RouteLocator locator, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyList<RouteLocator>> FindPromotableAsync(int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RouteLocator>>([]);
        public Task<int> CleanupAsync(int limit, CancellationToken cancellationToken) { Cleanups++; return Task.FromResult(0); }
    }
    private sealed class Authorized : ISessionCaptureAuthorization
    {
        public Task<string?> AuthorizeAsync(CapturedTurn turn, CancellationToken cancellationToken) => Task.FromResult<string?>("project");
    }
    private sealed class NoAccess : ISessionCaptureAuthorization
    {
        public Task<string?> AuthorizeAsync(CapturedTurn turn, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    }
    private sealed class DelayedExtraction : IMemoryExtractionService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<IReadOnlyList<MemoryCandidate>> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<MemoryCandidate>> ExtractAsync(string user, string assistant, CancellationToken cancellationToken)
        { Started.SetResult(); return Result.Task.WaitAsync(cancellationToken); }
    }
    private sealed class MutableMonitor : IOptionsMonitor<SessionCaptureOptions>
    {
        public SessionCaptureOptions CurrentValue { get; set; } = new();
        public SessionCaptureOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<SessionCaptureOptions, string?> listener) => null;
    }
    private sealed class Queue : ISessionCaptureRepository
    {
        public List<string> Calls { get; } = [];
        public CapturedTurn? Turn { get; init; }
        public Action? OnClaim { get; init; }
        public Task CleanupAsync(CancellationToken cancellationToken) { Calls.Add("cleanup"); return Task.CompletedTask; }
        public Task<CapturedTurn?> ClaimAsync(CancellationToken cancellationToken)
        { Assert.Equal("cleanup", Calls[^1]); Calls.Add("claim"); OnClaim?.Invoke(); return Task.FromResult(Turn); }
        public Task<SessionCaptureReceipt> EnqueueAsync(SessionCaptureEvent message, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SessionCaptureReceipt> EnqueueTurnAsync(SessionCaptureEvent user, SessionCaptureEvent assistant, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task FinishAsync(CapturedTurn turn, CaptureProcessingResult result, CancellationToken cancellationToken)
        { Assert.Same(Turn, turn); Calls.Add($"finish:{result.ResultCode}:{result.Disposition}"); return Task.CompletedTask; }
    }
}
