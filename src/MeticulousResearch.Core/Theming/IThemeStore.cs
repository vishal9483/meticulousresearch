namespace MeticulousResearch.Core.Theming;

/// <summary>
/// Persists the user's selected theme so it survives an app restart. Backed by
/// <c>ISettingsService</c> once settings-secure-key lands, or a local setting until then
/// (design-system-theming/phase.md).
/// </summary>
public interface IThemeStore
{
    /// <summary>Returns the saved selection, or <c>null</c> if none has been persisted yet.</summary>
    AppTheme? Load();

    /// <summary>Persists the selected theme.</summary>
    void Save(AppTheme theme);
}
