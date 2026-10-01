using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Exceptions;

namespace ContextDepot.Application.Tests.Contexts;

public sealed class ContextItemTests
{
    [Fact]
    public void Validity_requires_a_nonempty_interval()
    {
        var context = CreateContext();
        var now = DateTimeOffset.UtcNow;

        Assert.Equal(DomainErrorCodes.InvalidContextValidity,
            Assert.Throws<ContextDepotBusinessException>(() => context.SetValidity(now, now, null)).ErrorCode);
        Assert.Equal(DomainErrorCodes.InvalidContextValidity,
            Assert.Throws<ContextDepotBusinessException>(() => context.SetValidity(now.AddMinutes(1), now, null)).ErrorCode);
        Assert.Null(context.ValidFrom);
        Assert.Null(context.ValidUntil);

        context.SetValidity(now, now.AddMinutes(1), null);
        Assert.Equal(now, context.ValidFrom);
        Assert.Equal(now.AddMinutes(1), context.ValidUntil);
    }

    [Fact]
    public void Quality_rejects_values_outside_its_supported_range()
    {
        var context = CreateContext();

        Assert.Equal(DomainErrorCodes.InvalidContextQuality,
            Assert.Throws<ContextDepotBusinessException>(() => context.SetQuality(-1, null)).ErrorCode);
        Assert.Equal(DomainErrorCodes.InvalidContextQuality,
            Assert.Throws<ContextDepotBusinessException>(() => context.SetQuality(50, 1.1m)).ErrorCode);
        Assert.Equal((short)50, context.Importance);
        Assert.Null(context.Confidence);
    }

    private static ContextItem CreateContext() => new(
        Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
        ContextKind.Fact, null, null, "content", DateTimeOffset.UtcNow);
}
