namespace MeticulousResearch.Core.Environment;

/// <summary>Production <see cref="IEnvironment"/> backed by the real process environment.</summary>
public sealed class SystemEnvironment : IEnvironment
{
    public string? GetEnvironmentVariable(string name) =>
        System.Environment.GetEnvironmentVariable(name);
}
