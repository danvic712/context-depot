using Microsoft.Extensions.Options;

namespace ContextDepot.Application.Tests;

internal sealed class StaticOptionsSnapshot<T>(T value) : IOptionsSnapshot<T> where T : class
{
    public T Value => value;

    public T Get(string? name) => value;
}
