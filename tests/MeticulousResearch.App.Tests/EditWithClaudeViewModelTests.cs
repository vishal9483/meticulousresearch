using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Models;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit tests for <see cref="EditWithClaudeViewModel"/> (edit-with-claude/tests.md, SPEC §3.4). The
/// service-level assembly/provenance/streaming behaviour is covered window-free in the Core tests;
/// these cover the prompt-bar surface that turns a validation or generation failure into an
/// actionable message that commits no version.
/// </summary>
public sealed class EditWithClaudeViewModelTests
{
    private const string CatalogJson = """
    {
      "defaultModel": "claude-opus-5",
      "tiers": [
        { "tier": "Deep", "name": "Claude Opus 5", "id": "claude-opus-5", "contextTokens": 1000000, "maxOutputTokens": 128000, "priceInputMTok": 5, "priceOutputMTok": 25, "vision": true }
      ],
      "additional": []
    }
    """;

    private static IModelCatalog Catalog() => ModelCatalogLoader.Load(CatalogJson).Catalog;

    // Scenario: A follow-up instruction is required
    //   When I trigger "Edit with Claude" with an empty instruction
    //   Then I see a validation error
    //   And no new version is created
    [Fact]
    public async Task A_follow_up_instruction_is_required()
    {
        var service = new ThrowingEditService(
            new ArtifactValidationException("A follow-up instruction is required to edit with Claude."));
        var vm = new EditWithClaudeViewModel("artifact-1", service, Catalog()) { Instruction = "   " };

        await vm.EditCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("instruction", vm.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(vm.LastCommittedVersion);
    }

    // Scenario: A failed Claude edit surfaces an error and creates no version
    //   Given the FakeChatService is scripted to return an error
    //   When I ask Claude to revise the artifact
    //   Then I see an actionable error message
    //   And no new version is created
    [Fact]
    public async Task A_failed_claude_edit_surfaces_an_error_and_creates_no_version()
    {
        var service = new ThrowingEditService(
            new InvalidOperationException("The model service is temporarily unavailable. Try again."));
        var vm = new EditWithClaudeViewModel("artifact-1", service, Catalog()) { Instruction = "revise" };

        await vm.EditCommand.ExecuteAsync(null);

        Assert.Equal("The model service is temporarily unavailable. Try again.", vm.ErrorMessage);
        Assert.Null(vm.LastCommittedVersion);
    }

    // A successful edit surfaces no error and records the committed version (guards against a
    // tautological always-error surface).
    [Fact]
    public async Task A_successful_edit_records_the_committed_version_with_no_error()
    {
        var version = new ArtifactVersion { Id = "v2", ArtifactId = "artifact-1", VersionNo = 2, Content = "revised" };
        var vm = new EditWithClaudeViewModel("artifact-1", new SucceedingEditService(version), Catalog())
        {
            Instruction = "revise",
        };

        await vm.EditCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.Equal(2, vm.LastCommittedVersion);
    }

    private sealed class ThrowingEditService : IEditWithClaudeService
    {
        private readonly Exception _error;
        public ThrowingEditService(Exception error) => _error = error;

        public Task<ArtifactVersion> EditWithClaude(
            string artifactId, string instruction, string model,
            IProgress<string>? preview = null, CancellationToken cancellationToken = default)
            => Task.FromException<ArtifactVersion>(_error);

        public ArtifactVersion? SaveManualEdit(string artifactId, string content) => null;
    }

    private sealed class SucceedingEditService : IEditWithClaudeService
    {
        private readonly ArtifactVersion _version;
        public SucceedingEditService(ArtifactVersion version) => _version = version;

        public Task<ArtifactVersion> EditWithClaude(
            string artifactId, string instruction, string model,
            IProgress<string>? preview = null, CancellationToken cancellationToken = default)
        {
            preview?.Report(_version.Content);
            return Task.FromResult(_version);
        }

        public ArtifactVersion? SaveManualEdit(string artifactId, string content) => _version;
    }
}
