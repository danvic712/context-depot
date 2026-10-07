namespace ContextDepot.Application.Settings.Contracts;

public interface IAccessKeySecretGenerator
{
    GeneratedAccessKey Generate();
}
