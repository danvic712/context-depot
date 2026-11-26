using ContextDepot.Application.Contexts;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using Moq;

namespace ContextDepot.Application.Tests.Contexts;

public sealed class ContextManagementAppServiceTests
{
    [Fact]
    public async Task Management_denial_and_invalid_versions_never_reach_storage()
    {

        var access = new Mock<IWorkspaceAccessContext>();
        var repository = new Mock<IContextManagementRepository>();
        var service = new ContextManagementAppService(access.Object, repository.Object);
        var denied = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.DeleteAsync(Guid.CreateVersion7(), DateTimeOffset.UtcNow, default));
        Assert.Equal(ApplicationErrorCodes.SettingsForbidden, denied.ErrorCode);
        access.SetupGet(x => x.HasUnrestrictedAccess).Returns(true);
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.DeleteAsync(Guid.CreateVersion7(), default, default));
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Stale_version_is_reported_as_a_conflict_with_server_selected_depot()
    {
        var id = Guid.CreateVersion7(); var stamp = DateTimeOffset.UtcNow;

        var access = new Mock<IWorkspaceAccessContext>(); access.SetupGet(x => x.HasUnrestrictedAccess).Returns(true);
        var repository = new Mock<IContextManagementRepository>();
        repository.Setup(x => x.DeleteAsync(id, stamp, default)).ReturnsAsync(false);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            new ContextManagementAppService(access.Object, repository.Object).DeleteAsync(id, stamp, default));
        Assert.Equal(ApplicationErrorCodes.KnowledgeDeleteConflict, error.ErrorCode);
        repository.VerifyAll();
    }
}
