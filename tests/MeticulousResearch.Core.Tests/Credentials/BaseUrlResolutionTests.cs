using MeticulousResearch.Core.Credentials;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Credentials;

/// <summary>
/// @unit scenarios for API base URL / endpoint resolution (env wins), from
/// docs/features/settings-secure-key/tests.md — "API base URL / endpoint resolution".
/// </summary>
public sealed class BaseUrlResolutionTests
{
    private static ApiCredentialProvider NewProvider(FakeEnvironment env, StubSettings settings)
        => new(env, new FakeSecureKeyStore(), settings);

    // @unit
    // Scenario: The base URL defaults to the public Anthropic API when nothing is configured
    [Fact]
    public void Base_url_defaults_to_public_anthropic_api()
    {
        var env = new FakeEnvironment(); // ANTHROPIC_BASE_URL not set
        var settings = new StubSettings { ApiBaseUrl = null };
        var provider = NewProvider(env, settings);

        Assert.Equal(AnthropicApi.DefaultBaseUrl, provider.ResolveBaseUrl());
    }

    // @unit
    // Scenario: A persisted base URL setting overrides the default
    [Fact]
    public void Persisted_setting_overrides_default()
    {
        var env = new FakeEnvironment(); // not set
        var settings = new StubSettings { ApiBaseUrl = "https://llm.example.internal" };
        var provider = NewProvider(env, settings);

        Assert.Equal("https://llm.example.internal", provider.ResolveBaseUrl());
    }

    // @unit
    // Scenario: The ANTHROPIC_BASE_URL environment variable takes precedence over the setting
    [Fact]
    public void Env_base_url_takes_precedence_over_setting()
    {
        var env = new FakeEnvironment().Set("ANTHROPIC_BASE_URL", "https://llm.sdc.siemens.cloud");
        var settings = new StubSettings { ApiBaseUrl = "https://llm.example.internal" };
        var provider = NewProvider(env, settings);

        Assert.Equal("https://llm.sdc.siemens.cloud", provider.ResolveBaseUrl());
        Assert.True(provider.IsBaseUrlFromEnvironment);
    }

    // @unit
    // Scenario: A base URL supplied via the environment is shown as an override and not persisted
    // (resolution + non-persistence half; the "shown as environment-provided" half is asserted in
    // the SettingsViewModel @unit test.)
    [Fact]
    public void Env_base_url_is_flagged_as_override_and_not_persisted()
    {
        var env = new FakeEnvironment().Set("ANTHROPIC_BASE_URL", "https://llm.sdc.siemens.cloud");
        var settings = new StubSettings { ApiBaseUrl = null };
        var provider = NewProvider(env, settings);

        Assert.Equal("https://llm.sdc.siemens.cloud", provider.ResolveBaseUrl());
        Assert.True(provider.IsBaseUrlFromEnvironment);
        // the persisted base URL setting remains unset
        Assert.Null(settings.ApiBaseUrl);
    }
}
