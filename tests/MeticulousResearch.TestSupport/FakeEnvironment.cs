using MeticulousResearch.Core.Environment;

namespace MeticulousResearch.TestSupport;

/// <summary>
/// In-memory <see cref="IEnvironment"/> for tests. Variables can be set, overwritten, and
/// cleared so credential/endpoint resolution ("env wins") is exercised deterministically.
/// </summary>
public sealed class FakeEnvironment : IEnvironment
{
    private readonly Dictionary<string, string?> _vars = new(StringComparer.Ordinal);

    public string? GetEnvironmentVariable(string name) =>
        _vars.TryGetValue(name, out var value) ? value : null;

    /// <summary>Sets (or overwrites) a variable. Pass <c>null</c> or call <see cref="Clear"/> to unset.</summary>
    public FakeEnvironment Set(string name, string? value)
    {
        if (value is null)
            _vars.Remove(name);
        else
            _vars[name] = value;
        return this;
    }

    public FakeEnvironment Clear(string name)
    {
        _vars.Remove(name);
        return this;
    }
}
