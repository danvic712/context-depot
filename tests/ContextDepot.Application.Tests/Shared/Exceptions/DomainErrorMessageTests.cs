using ContextDepot.Domain;

namespace ContextDepot.Application.Tests.Shared.Exceptions;

public sealed class DomainErrorMessageTests
{
    [Fact]
    public void Invalid_enum_values_use_a_user_friendly_locale_message()
    {
        var message = DomainErrorMessages.ExpectedEnumValue("ContextKind");

        Assert.Contains("ContextKind", message);
        Assert.StartsWith("请选择有效的", message, StringComparison.Ordinal);
    }
}
