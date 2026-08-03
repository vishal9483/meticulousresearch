using MeticulousResearch.Core.Theming;

namespace MeticulousResearch.TestSupport;

/// <summary>
/// In-memory <see cref="IThemeStore"/> for tests. Persistence "survives" a simulated restart by
/// reusing the same store instance across two <c>ThemeService</c> constructions.
/// </summary>
public sealed class FakeThemeStore : IThemeStore
{
    private AppTheme? _theme;

    /// <summary>Creates a store, optionally pre-seeded with a persisted selection.</summary>
    public FakeThemeStore(AppTheme? seed = null) => _theme = seed;

    public AppTheme? Load() => _theme;

    public void Save(AppTheme theme) => _theme = theme;
}
