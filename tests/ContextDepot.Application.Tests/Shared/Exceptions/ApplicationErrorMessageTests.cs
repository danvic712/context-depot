using System.Reflection;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Localization;
using ContextDepot.Domain.Exceptions;

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
        var catalog = new EmbeddedLocaleCatalog(typeof(ApplicationErrorMessages).Assembly);
        Assert.All(codes, code =>
        {
            Assert.True(ApplicationErrorMessages.Contains(code), $"Missing message for {code}.");
            Assert.False(string.IsNullOrWhiteSpace(ApplicationErrorMessages.Get(code)));
            Assert.True(catalog.GetMessages("en-US").ContainsKey(code), $"Missing English message for {code}.");
            Assert.True(catalog.GetMessages("zh-CN").ContainsKey(code), $"Missing Chinese message for {code}.");
        });
    }

    [Fact]
    public void Application_exception_keeps_a_stable_error_code()
    {
        var exception = new ContextDepotApplicationException(ApplicationErrorCodes.DocumentConflict);

        Assert.Equal(ApplicationErrorCodes.DocumentConflict, exception.ErrorCode);
        Assert.Equal(ApplicationErrorCodes.DocumentConflict, exception.Message);
    }

    [Theory]
    [InlineData("工作空间不存在")]
    [InlineData("The workspace does not exist.")]
    [InlineData("")]
    public void Business_exception_rejects_human_messages(string message)
    {
        Assert.Throws<ArgumentException>(() => new ContextDepotBusinessException(message));
    }

    [Fact]
    public void Application_exception_requires_a_registered_code()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContextDepotApplicationException("UnregisteredCode"));
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
