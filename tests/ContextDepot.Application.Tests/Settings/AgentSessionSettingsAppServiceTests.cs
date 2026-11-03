using System.Text.Json;
using ContextDepot.Application.Settings;
using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Settings.Validators;
using ContextDepot.Application.Shared.Exceptions;
using Moq;

namespace ContextDepot.Application.Tests.Settings;

public sealed class AgentSessionSettingsAppServiceTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Enabled\":null}")]
    public async Task Missing_or_null_enabled_is_invalid_and_never_writes(string json)
    {
        var repository = new Mock<IAgentSessionSettingsRepository>(MockBehavior.Strict);
        var request = JsonSerializer.Deserialize<SaveAgentSessionSettingsRequest>(json)!;
        var service = new AgentSessionSettingsAppService(repository.Object, new SaveAgentSessionSettingsRequestValidator());
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.SaveAsync(request, default));
        Assert.Equal(ApplicationErrorCodes.InvalidRequest, error.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("\"true\"")]
    [InlineData("1")]
    public void Non_boolean_request_is_rejected(string value) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SaveAgentSessionSettingsRequest>(
            "{\"Enabled\":" + value + "}"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_writes_explicit_boolean_then_reads_authoritative_value(bool enabled)
    {
        var sequence = new MockSequence();
        var repository = new Mock<IAgentSessionSettingsRepository>(MockBehavior.Strict);
        repository.InSequence(sequence).Setup(r => r.SetEnabledAsync(enabled, default)).Returns(Task.CompletedTask);
        var authoritative = new AgentSessionSettingsDto(!enabled);
        repository.InSequence(sequence).Setup(r => r.GetAsync(default)).ReturnsAsync(authoritative);
        var service = new AgentSessionSettingsAppService(repository.Object, new SaveAgentSessionSettingsRequestValidator());
        Assert.Same(authoritative, await service.SaveAsync(new(enabled), default));
        repository.VerifyAll();
    }

    [Fact]
    public async Task Save_failure_is_not_reported_as_success()
    {
        var repository = new Mock<IAgentSessionSettingsRepository>(MockBehavior.Strict);
        repository.Setup(r => r.SetEnabledAsync(true, default)).ThrowsAsync(new InvalidOperationException("refresh failed"));
        var service = new AgentSessionSettingsAppService(repository.Object, new SaveAgentSessionSettingsRequestValidator());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(new(true), default));
        repository.Verify(r => r.GetAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Get_returns_database_value()
    {
        var repository = new Mock<IAgentSessionSettingsRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetAsync(default)).ReturnsAsync(new AgentSessionSettingsDto(false));
        var service = new AgentSessionSettingsAppService(repository.Object, new SaveAgentSessionSettingsRequestValidator());
        Assert.False((await service.GetAsync(default)).Enabled);
    }
}
