using ContextDepot.Application.Settings;
using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Settings.Enums;
using ContextDepot.Application.Settings.Validators;
using ContextDepot.Application.Shared.Exceptions;
using FluentValidation;
using Moq;

namespace ContextDepot.Application.Tests.Settings;

public sealed class AppearanceSettingsAppServiceTests
{
    [Theory]
    [InlineData(Theme.System)]
    [InlineData(Theme.Light)]
    [InlineData(Theme.Dark)]
    public async Task ThemeUpdateOnlyWritesThemeAsync(Theme theme)
    {
        var repository = new Mock<IAppearanceSettingsRepository>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        repository.Setup(x => x.SetThemeAsync(theme, cancellation.Token)).Returns(Task.CompletedTask);
        await CreateService(repository.Object).SetThemeAsync(new ThemeRequest(theme), cancellation.Token);
        repository.VerifyAll();
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(Language.ZhCn)]
    [InlineData(Language.EnUs)]
    public async Task LanguageUpdateOnlyWritesLanguageAsync(Language language)
    {
        var repository = new Mock<IAppearanceSettingsRepository>(MockBehavior.Strict);
        repository.Setup(x => x.SetLanguageAsync(language, CancellationToken.None)).Returns(Task.CompletedTask);
        await CreateService(repository.Object).SetLanguageAsync(new LanguageRequest(language), CancellationToken.None);
        repository.VerifyAll();
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData((Theme)(-1))]
    [InlineData((Theme)99)]
    public async Task InvalidThemeDoesNotWriteAsync(Theme? theme)
    {
        var repository = new Mock<IAppearanceSettingsRepository>(MockBehavior.Strict);
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService(repository.Object).SetThemeAsync(new ThemeRequest(theme), CancellationToken.None));
        Assert.Equal(ApplicationErrorCodes.InvalidAppearanceTheme, Assert.Single(error.Errors).ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData((Language)(-1))]
    [InlineData((Language)99)]
    public async Task InvalidLanguageDoesNotWriteAsync(Language? language)
    {
        var repository = new Mock<IAppearanceSettingsRepository>(MockBehavior.Strict);
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService(repository.Object).SetLanguageAsync(new LanguageRequest(language), CancellationToken.None));
        Assert.Equal(ApplicationErrorCodes.InvalidAppearanceLanguage, Assert.Single(error.Errors).ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadsStoredDefaultsAsync()
    {
        var expected = new AppearanceSettingsDto(Theme.Dark, Language.EnUs);
        var repository = new Mock<IAppearanceSettingsRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetAsync(CancellationToken.None)).ReturnsAsync(expected);
        Assert.Equal(expected, await CreateService(repository.Object).GetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PersistenceFailureIsNotReportedAsSuccessAsync()
    {
        var repository = new Mock<IAppearanceSettingsRepository>(MockBehavior.Strict);
        repository.Setup(x => x.SetThemeAsync(Theme.Dark, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("unavailable"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(repository.Object).SetThemeAsync(new ThemeRequest(Theme.Dark), CancellationToken.None));
    }

    private static AppearanceSettingsAppService CreateService(IAppearanceSettingsRepository repository) =>
        new(repository, new ThemeRequestValidator(), new LanguageRequestValidator());
}
