using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Models;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// The "Edit with Claude" prompt bar in the artifact editor (SPEC §3.4, §9.1(5)): an instruction
/// input, a per-edit model selector, a streaming preview, and commit/keep vs. discard. Window-free so
/// its flow is <c>@unit</c>-testable; the iteration engine itself is owned by
/// <see cref="IEditWithClaudeService"/>. On success it commits a new Claude-authored version through
/// artifact-versioning; validation and generation failures surface an actionable message and commit
/// nothing.
/// </summary>
public sealed partial class EditWithClaudeViewModel : ObservableObject
{
    private readonly IEditWithClaudeService _service;
    private readonly string _artifactId;

    /// <summary>Creates the prompt bar over the edit service and per-edit model picker.</summary>
    /// <param name="artifactId">The artifact this bar edits.</param>
    /// <param name="service">The edit-with-Claude iteration engine.</param>
    /// <param name="catalog">The model catalog backing the per-edit model selector.</param>
    /// <exception cref="ArgumentNullException">A required collaborator is null.</exception>
    public EditWithClaudeViewModel(string artifactId, IEditWithClaudeService service, IModelCatalog catalog)
    {
        _artifactId = artifactId ?? throw new ArgumentNullException(nameof(artifactId));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        ModelPicker = new ModelPickerViewModel(catalog ?? throw new ArgumentNullException(nameof(catalog)));
    }

    /// <summary>The per-edit model selector (a fresh model can be chosen for each edit).</summary>
    public ModelPickerViewModel ModelPicker { get; }

    /// <summary>The follow-up instruction the analyst types.</summary>
    [ObservableProperty]
    private string _instruction = "";

    /// <summary>The revised content as it streams from the model (the preview).</summary>
    [ObservableProperty]
    private string _previewContent = "";

    /// <summary>An actionable error/validation message, or null when the last edit was clean.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Whether an edit is currently in progress.</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>The version number of the last committed edit, or null when none has committed.</summary>
    [ObservableProperty]
    private long? _lastCommittedVersion;

    /// <summary>
    /// Runs the follow-up instruction through Claude, streaming into <see cref="PreviewContent"/> and
    /// committing a new version only on success. Validation and failure set
    /// <see cref="ErrorMessage"/> and commit nothing.
    /// </summary>
    [RelayCommand]
    public async Task EditAsync()
    {
        ErrorMessage = null;
        PreviewContent = "";
        IsBusy = true;
        try
        {
            var preview = new Progress<string>(text => PreviewContent = text);
            var version = await _service
                .EditWithClaude(_artifactId, Instruction, ModelPicker.CurrentModelId, preview)
                .ConfigureAwait(true);
            LastCommittedVersion = version.VersionNo;
        }
        catch (OperationCanceledException)
        {
            // Cancelled edits commit nothing and leave the current version intact.
        }
        catch (ArtifactValidationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
