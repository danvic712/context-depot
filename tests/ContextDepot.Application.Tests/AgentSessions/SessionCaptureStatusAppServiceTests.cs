using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using Moq;

namespace ContextDepot.Application.Tests.AgentSessions;

public sealed class SessionCaptureStatusAppServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Requires_exactly_one_receipt(bool both)
    {
        var repository = new Mock<ISessionCaptureStatusRepository>(MockBehavior.Strict);
        var service = new SessionCaptureStatusAppService(Mock.Of<ICurrentDepotContext>(), Mock.Of<IWorkspaceAccessContext>(), repository.Object);
        var request = both ? new SessionCaptureStatusRequest(Guid.NewGuid(), Guid.NewGuid()) : new();
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.GetAsync(request, default));
        Assert.Equal(ApplicationErrorCodes.InvalidSessionCapture, error.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Status_uses_authenticated_identity_and_returns_repository_snapshot()
    {
        var depot = Guid.NewGuid(); var key = Guid.NewGuid(); var receipt = Guid.NewGuid();
        var repository = new Mock<ISessionCaptureStatusRepository>();
        var request = new SessionCaptureStatusRequest(receipt);
        var expected = new SessionCaptureStatusResult(receipt, null, "not_applicable", "completed", "saved", null,
            "development", [], [], DateTimeOffset.UtcNow, null, null, 0, 1);
        repository.Setup(r => r.GetAsync(depot, key, request, default)).ReturnsAsync(expected);
        var service = new SessionCaptureStatusAppService(Mock.Of<ICurrentDepotContext>(x => x.DepotId == depot),
            Mock.Of<IWorkspaceAccessContext>(x => x.DepotAccessKeyId == key), repository.Object);
        Assert.Same(expected, await service.GetAsync(request, default));
        repository.Verify(r => r.GetAsync(depot, key, request, default), Times.Once);
    }

    [Fact]
    public async Task Missing_or_foreign_receipt_is_uniformly_unavailable()
    {
        var depot = Guid.NewGuid(); var key = Guid.NewGuid();
        var repository = new Mock<ISessionCaptureStatusRepository>();
        var service = new SessionCaptureStatusAppService(Mock.Of<ICurrentDepotContext>(x => x.DepotId == depot),
            Mock.Of<IWorkspaceAccessContext>(x => x.DepotAccessKeyId == key), repository.Object);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.GetAsync(new(Guid.NewGuid()), default));
        Assert.Equal(ApplicationErrorCodes.SessionCaptureUnavailable, error.ErrorCode);
    }
}
