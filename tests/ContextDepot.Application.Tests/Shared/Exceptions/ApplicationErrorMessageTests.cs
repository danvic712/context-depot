using System.Reflection;
using ContextDepot.Application.Shared.Exceptions;

namespace ContextDepot.Application.Tests.Shared.Exceptions;

public sealed class ApplicationErrorMessageTests
{
    [Fact]
    public void Every_application_error_code_has_a_catalog_message()
    {
        var codes = typeof(ApplicationErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        Assert.NotEmpty(codes);
        Assert.All(codes, code =>
        {
            Assert.True(ApplicationErrorMessages.Contains(code), $"Missing message for {code}.");
            Assert.False(string.IsNullOrWhiteSpace(ApplicationErrorMessages.Get(code)));
        });
    }

    [Fact]
    public void Application_exception_keeps_a_stable_error_code()
    {
        var exception = new ContextDepotApplicationException(ApplicationErrorCodes.DocumentConflict);

        Assert.Equal(ApplicationErrorCodes.DocumentConflict, exception.ErrorCode);
        Assert.Equal(ApplicationErrorCodes.DocumentConflict, exception.Message);
    }

    [Fact]
    public void Error_messages_use_the_explicit_locale()
    {
        var code = ApplicationErrorCodes.DocumentConflict;

        Assert.NotEqual(
            ApplicationErrorMessages.Get(code, "zh-CN"),
            ApplicationErrorMessages.Get(code, "en-US"));
    }
}
