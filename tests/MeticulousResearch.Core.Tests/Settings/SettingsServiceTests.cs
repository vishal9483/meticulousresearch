using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Settings;

/// <summary>
/// @unit / @integration scenarios for app settings persistence and defaults
/// (docs/features/settings-secure-key/tests.md — "App settings").
/// </summary>
public sealed class SettingsServiceTests
{
    private static DataStore NewStore(TempDataDirectory temp)
    {
        var store = new DataStore(new FakeClock(), temp.Path);
        store.Initialize();
        return store;
    }

    // @unit @integration
    // Scenario Outline: Settings persist across restart
    //   Examples: default model / theme / context budget / telemetry / api base url
    [Theory]
    [InlineData("default model", "claude-opus-5")]
    [InlineData("theme", "dark")]
    [InlineData("context budget", "150000")]
    [InlineData("telemetry", "off")]
    [InlineData("api base url", "https://llm.example.internal")]
    public void Settings_persist_across_restart(string setting, string value)
    {
        using var temp = new TempDataDirectory();
        var store = NewStore(temp);

        // set "<setting>" to "<value>"
        Set(new SettingsService(store), setting, value);

        // the app restarts (a fresh service over the same store)
        var afterRestart = new SettingsService(store);

        // "<setting>" is still "<value>"
        Assert.Equal(value, Get(afterRestart, setting));
    }

    // @unit
    // Scenario: Telemetry is off by default
    [Fact]
    public void Telemetry_is_off_by_default()
    {
        using var temp = new TempDataDirectory();
        var settings = new SettingsService(NewStore(temp));

        Assert.False(settings.TelemetryEnabled);
    }

    // @unit
    // Scenario: Default model defaults to Claude Opus 5
    [Fact]
    public void Default_model_defaults_to_claude_opus_5()
    {
        using var temp = new TempDataDirectory();
        var settings = new SettingsService(NewStore(temp));

        Assert.Equal("claude-opus-5", settings.DefaultModel);
    }

    private static void Set(ISettingsService s, string setting, string value)
    {
        switch (setting)
        {
            case "default model": s.DefaultModel = value; break;
            case "theme": s.Theme = value; break;
            case "context budget": s.ContextBudget = int.Parse(value); break;
            case "telemetry": s.TelemetryEnabled = value != "off"; break;
            case "api base url": s.ApiBaseUrl = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(setting), setting, "Unknown setting.");
        }
    }

    private static string Get(ISettingsService s, string setting) => setting switch
    {
        "default model" => s.DefaultModel,
        "theme" => s.Theme,
        "context budget" => s.ContextBudget.ToString(),
        "telemetry" => s.TelemetryEnabled ? "on" : "off",
        "api base url" => s.ApiBaseUrl ?? "",
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "Unknown setting."),
    };
}
