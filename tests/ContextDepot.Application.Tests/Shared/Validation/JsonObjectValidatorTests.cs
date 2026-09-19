using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Validation;

namespace ContextDepot.Application.Tests.Shared.Validation;

public sealed class JsonObjectValidatorTests
{
    [Fact]
    public void Object_json_is_accepted()
    {
        var exception = Record.Exception(() => JsonObjectValidator.EnsureObject("{\"enabled\":true}", ApplicationErrorCodes.InvalidContextMetadata));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("not-json")]
    public void Non_object_json_uses_the_supplied_error_code(string json)
    {
        var exception = Assert.Throws<ContextDepotApplicationException>(() =>
            JsonObjectValidator.EnsureObject(json, ApplicationErrorCodes.InvalidWorkspaceMetadata));

        Assert.Equal(ApplicationErrorCodes.InvalidWorkspaceMetadata, exception.ErrorCode);
    }
}
