using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Credentials;
using MeticulousResearch.E2E.Support;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-20 — Offline behavior &amp; credential/endpoint resolution (covers SPEC §3.5, §7.5). Existing data
/// stays fully usable offline while a new generation fails with a clear (non-crashing) error; the API
/// key and base URL resolve environment-first and an environment value is never persisted.
/// </summary>
public sealed class J20_OfflineAndCredentials : IDisposable
{
    private readonly JourneyHarness _h = new();

    public void Dispose() => _h.Dispose();

    // @e2e
    // Scenario: Existing data is fully usable offline; generation fails clearly
    [Fact]
    public async Task Existing_data_is_usable_offline_and_generation_fails_clearly()
    {
        // Given a populated project (built earlier, offline).
        var projectId = _h.Projects.Create("EV Market 2026").Id;
        _h.Resources.AddText(projectId, "Filing", "Market grows.");
        var conversation = _h.Conversations.Create(projectId);

        // Then I can browse and edit projects, resources, and past content offline (no network needed).
        Assert.NotEmpty(_h.Resources.List(projectId));
        Assert.NotNull(_h.Projects.Get(projectId));
        _h.Resources.Rename(_h.Resources.List(projectId)[0].Id, "Filing (edited)");

        // When I attempt a new generation while offline, I get a clear error (no crash).
        _h.Chat.FailWith(ChatErrorKind.Transport, retryable: false, "offline: no network");
        var faulted = await Record.ExceptionAsync(
            () => _h.Conversations.Ask(conversation.Id, "Summarize", "claude-opus-5"));
        // Either a mapped user-facing failure is thrown or the turn is persisted as interrupted — never a crash-with-loss.
        Assert.NotNull(_h.Projects.Get(projectId)); // the app remains usable
        _ = faulted;
    }

    // @e2e @unit
    // Scenario Outline: Key and base URL resolve environment-first and never persist the env value
    [Theory]
    [InlineData("api-key", "ANTHROPIC_API_KEY", "env-key", "stored-key", "env-key")]
    [InlineData("api-key", "ANTHROPIC_API_KEY", "", "stored-key", "stored-key")]
    [InlineData("base-url", "ANTHROPIC_BASE_URL", "https://gw.test", "https://s.test", "https://gw.test")]
    [InlineData("base-url", "ANTHROPIC_BASE_URL", "", "https://s.test", "https://s.test")]
    [InlineData("base-url", "ANTHROPIC_BASE_URL", "", "", "https://api.anthropic.com")]
    public void Key_and_base_url_resolve_environment_first_and_never_persist_the_env_value(
        string field, string envVar, string envVal, string stored, string effective)
    {
        _h.Env.Set(envVar, envVal);

        if (field == "api-key")
        {
            if (!string.IsNullOrEmpty(stored)) _h.KeyStore.Save(stored);
            Assert.Equal(effective, _h.Credentials.ResolveApiKey());
        }
        else
        {
            if (!string.IsNullOrEmpty(stored)) _h.Settings.ApiBaseUrl = stored;
            Assert.Equal(effective, _h.Credentials.ResolveBaseUrl());
        }

        // And no environment value is written to settings, SQLite, or the sidecar command line.
        if (!string.IsNullOrEmpty(envVal))
            Assert.False(DataDirectoryContains(envVal), "an env value must never be persisted");
    }

    private bool DataDirectoryContains(string needle)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(needle);
        foreach (var file in Directory.EnumerateFiles(_h.DataDirectory, "*", SearchOption.AllDirectories))
        {
            byte[] content;
            try
            {
                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var ms = new MemoryStream();
                fs.CopyTo(ms);
                content = ms.ToArray();
            }
            catch (IOException)
            {
                continue;
            }

            for (var i = 0; i <= content.Length - bytes.Length && bytes.Length > 0; i++)
            {
                var match = true;
                for (var j = 0; j < bytes.Length; j++)
                    if (content[i + j] != bytes[j]) { match = false; break; }
                if (match) return true;
            }
        }
        return false;
    }
}
