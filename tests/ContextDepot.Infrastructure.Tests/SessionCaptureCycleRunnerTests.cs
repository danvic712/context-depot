using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.AgentSessions.Enums;
using ContextDepot.Domain.AgentSessions;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Contexts.Enums;
using ContextDepot.Application.Shared.Safety;
using ContextDepot.Domain.Contexts.Enums;
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
        var options = new MutableMonitor();
        var queue = new Queue();
        var services = new ServiceCollection();
        services.AddScoped<CurrentDepotAccessContext>();
        services.AddSingleton<ISessionCaptureRepository>(queue);
        using var provider = services.BuildServiceProvider();
        var runner = new SessionCaptureCycleRunner(provider.GetRequiredService<IServiceScopeFactory>(), options,
            NullLogger<SessionCaptureCycleRunner>.Instance);
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
    }

    [Fact]
    public async Task Disabling_after_claim_allows_claimed_work_to_finish()
    {
        var options = new MutableMonitor { CurrentValue = new() { Enabled = true } };
        var turn = new CapturedTurn(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow.AddMinutes(1), "user", "assistant");
        var queue = new Queue { Turn = turn, OnClaim = () => options.CurrentValue = new() { Enabled = false } };
        var services = new ServiceCollection();
        services.AddScoped<CurrentDepotAccessContext>();
        services.AddSingleton<ISessionCaptureRepository>(queue);
        services.AddScoped(_ => new MemoryCaptureProcessor(new NoAccess(), null!, null!, null!));
        using var provider = services.BuildServiceProvider();
        var runner = new SessionCaptureCycleRunner(provider.GetRequiredService<IServiceScopeFactory>(), options,
            NullLogger<SessionCaptureCycleRunner>.Instance);
        await runner.RunAsync(default);
        Assert.False(options.CurrentValue.Enabled);
        Assert.Equal(["cleanup", "claim", "finish:skipped_unauthorized:Completed"], queue.Calls);
        await runner.RunAsync(default);
        Assert.Equal("cleanup", queue.Calls[^1]);
        Assert.Equal(4, queue.Calls.Count);
    }

    [Fact]
    public async Task Authorized_extraction_already_in_flight_can_save_after_disable()
    {
        const string quote = "项目的持久化数据保存在 PostgreSQL 数据库中。";
        var options = new MutableMonitor { CurrentValue = new() { Enabled = true } };
        var turn = new CapturedTurn(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow.AddMinutes(1), quote, "好的。");
        var queue = new Queue { Turn = turn };
        var extraction = new DelayedExtraction();
        var contexts = new SavingContexts();
        var services = new ServiceCollection();
        services.AddScoped<CurrentDepotAccessContext>();
        services.AddSingleton<ISessionCaptureRepository>(queue);
        services.AddScoped(_ => new MemoryCaptureProcessor(new Authorized(), extraction, contexts, new HighConfidenceSecretDetector()));
        using var provider = services.BuildServiceProvider();
        var runner = new SessionCaptureCycleRunner(provider.GetRequiredService<IServiceScopeFactory>(), options,
            NullLogger<SessionCaptureCycleRunner>.Instance);
        var running = runner.RunAsync(default);
        await extraction.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        options.CurrentValue = new() { Enabled = false };
        extraction.Result.SetResult([new MemoryCandidate("fact", "project.database", quote, quote, 0.95m)]);
        await running.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(options.CurrentValue.Enabled);
        Assert.Equal(quote, Assert.Single(contexts.Saved).Content);
        Assert.Equal(ContextWriteMode.CreateOnly, contexts.Saved[0].WriteMode);
        Assert.Equal(["cleanup", "claim", "finish:saved:Completed"], queue.Calls);
        await runner.RunAsync(default);
        Assert.Equal(["cleanup", "claim", "finish:saved:Completed", "cleanup"], queue.Calls);
    }

    private sealed class Authorized : ISessionCaptureAuthorization
    {
        public Task<string?> AuthorizeAsync(CapturedTurn turn, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("project");
    }

    private sealed class DelayedExtraction : IMemoryExtractionService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<IReadOnlyList<MemoryCandidate>> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<MemoryCandidate>> ExtractAsync(string user, string assistant, CancellationToken cancellationToken)
        {
            Started.SetResult();
            return Result.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class SavingContexts : IContextAppService
    {
        public List<SaveContextCommand> Saved { get; } = [];
        public Task<SaveContextResult> SaveAsync(SaveContextCommand command, CancellationToken cancellationToken)
        {
            Saved.Add(command);
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new SaveContextResult(new ContextModel(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
                command.Kind, command.Key, command.Title, command.Content, command.Tags ?? [], command.Importance,
                command.Confidence, ContextStatus.Active, command.VerificationStatus, command.RequestedProvenanceTrust,
                command.SourceType, command.SourceAgent, command.SourceRef, null, null, now, now), SaveContextOutcome.Created));
        }
        public Task ArchiveAsync(Guid contextId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoAccess : ISessionCaptureAuthorization
    {
        public Task<string?> AuthorizeAsync(CapturedTurn turn, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
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
        {
            Assert.Equal("cleanup", Calls[^1]);
            Calls.Add("claim");
            OnClaim?.Invoke();
            return Task.FromResult(Turn);
        }
        public Task<SessionCaptureReceipt> EnqueueAsync(SessionCaptureEvent message, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SessionCaptureReceipt> EnqueueTurnAsync(SessionCaptureEvent user, SessionCaptureEvent assistant, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task FinishAsync(CapturedTurn turn, string resultCode, SessionCaptureDisposition disposition, CancellationToken cancellationToken)
        {
            Assert.Same(Turn, turn);
            Calls.Add($"finish:{resultCode}:{disposition}");
            return Task.CompletedTask;
        }
    }
}
