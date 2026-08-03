using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.App.Services;
using MeticulousResearch.Core.Turns;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// The per-turn metadata, cost badge, and actions shown on a completed assistant turn
/// (turn-metadata-actions, SPEC §3.3, §3.6). Projects the turn's <see cref="TurnMetadata"/> (model,
/// tokens, latency, resource scope) for the expandable details panel, computes an inline cost badge
/// with an expandable itemised breakdown through the <see cref="ITurnCostCalculator"/> seam, and
/// exposes the turn actions — copy (to the clipboard), retry (same/other model), edit-and-resend,
/// promote-to-artifact, and delete — as commands wired to the supplied callbacks. Window-free so its
/// logic is <c>@unit</c>-testable.
/// </summary>
public sealed partial class TurnActionsViewModel : ObservableObject
{
    private readonly IClipboardService _clipboard;
    private readonly ITurnCostCalculator _costCalculator;
    private readonly Func<string?, Task>? _retry;
    private readonly Func<string, Task>? _editResend;
    private readonly Func<PromoteToArtifactRequest, Task>? _promote;
    private readonly Func<Task>? _delete;
    private readonly Func<PromoteToArtifactRequest>? _buildPromoteRequest;

    /// <summary>Creates the actions view-model for a completed assistant turn.</summary>
    /// <param name="assistantText">The assistant turn's text (the Copy/Promote payload).</param>
    /// <param name="metadata">The turn's projected metadata (model, tokens, latency, scope).</param>
    /// <param name="costCalculator">The per-turn cost seam pricing the badge/breakdown.</param>
    /// <param name="clipboard">The clipboard seam backing the Copy action.</param>
    /// <param name="retry">Regenerates the answer; the argument is an optional other-model id (null = same model).</param>
    /// <param name="editResend">Replaces the user message with the argument text and regenerates.</param>
    /// <param name="buildPromoteRequest">Builds the promote-to-artifact request when Promote fires.</param>
    /// <param name="promote">Consumes the promote-to-artifact request (M3 artifact-creation).</param>
    /// <param name="delete">Deletes the turn from the conversation.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public TurnActionsViewModel(
        string assistantText,
        TurnMetadata metadata,
        ITurnCostCalculator costCalculator,
        IClipboardService clipboard,
        Func<string?, Task>? retry = null,
        Func<string, Task>? editResend = null,
        Func<PromoteToArtifactRequest>? buildPromoteRequest = null,
        Func<PromoteToArtifactRequest, Task>? promote = null,
        Func<Task>? delete = null)
    {
        Text = assistantText ?? throw new ArgumentNullException(nameof(assistantText));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _costCalculator = costCalculator ?? throw new ArgumentNullException(nameof(costCalculator));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _retry = retry;
        _editResend = editResend;
        _buildPromoteRequest = buildPromoteRequest;
        _promote = promote;
        _delete = delete;
    }

    /// <summary>The assistant turn's text.</summary>
    public string Text { get; }

    /// <summary>The turn's projected metadata.</summary>
    public TurnMetadata Metadata { get; }

    /// <summary>The model id shown in the metadata panel (empty when unrecorded).</summary>
    public string ModelLabel => Metadata.Model ?? "";

    /// <summary>The billed input tokens shown in the metadata panel.</summary>
    public long InputTokens => Metadata.InputTokens;

    /// <summary>The billed output tokens shown in the metadata panel.</summary>
    public long OutputTokens => Metadata.OutputTokens;

    /// <summary>The end-to-end latency shown in the metadata panel (empty when unrecorded).</summary>
    public string LatencyLabel =>
        Metadata.LatencyMs is { } ms ? $"{ms} ms" : "";

    /// <summary>A comma-separated label of the resource ids that were in scope for the turn.</summary>
    public string ResourceScopeLabel => string.Join(", ", Metadata.ResourceScope);

    /// <summary>The itemised per-turn cost.</summary>
    public TurnCostBreakdown Cost => _costCalculator.Calculate(Metadata);

    /// <summary>The inline cost badge (the turn's total cost, e.g. <c>$0.18</c>).</summary>
    public string CostBadge => Cost.Total.ToString("C2", CultureInfo.CurrentCulture);

    /// <summary>The expanded cost breakdown itemising input/output/cache-read/cache-write contributions.</summary>
    public string CostBreakdownDetail
    {
        get
        {
            var c = Cost;
            return string.Join(
                System.Environment.NewLine,
                $"Input: {c.InputCost.ToString("C2", CultureInfo.CurrentCulture)}",
                $"Output: {c.OutputCost.ToString("C2", CultureInfo.CurrentCulture)}",
                $"Cache read: {c.CacheReadCost.ToString("C2", CultureInfo.CurrentCulture)}",
                $"Cache write: {c.CacheWriteCost.ToString("C2", CultureInfo.CurrentCulture)}");
        }
    }

    /// <summary>The edited user text bound by the edit-and-resend affordance (defaults to empty).</summary>
    [ObservableProperty]
    private string _editedText = "";

    /// <summary>The other-model id chosen for a retry-with-other-model (null/blank = same model).</summary>
    [ObservableProperty]
    private string? _retryModelId;

    /// <summary>Copies the assistant turn's text to the clipboard (SPEC §3.3).</summary>
    [RelayCommand]
    private void Copy() => _clipboard.SetText(Text);

    /// <summary>Regenerates the answer using the same model.</summary>
    [RelayCommand]
    private Task Retry() => _retry?.Invoke(null) ?? Task.CompletedTask;

    /// <summary>Regenerates the answer using the chosen other model (<see cref="RetryModelId"/>).</summary>
    [RelayCommand]
    private Task RetryWithOtherModel() => _retry?.Invoke(RetryModelId) ?? Task.CompletedTask;

    /// <summary>Replaces the user message with <see cref="EditedText"/> and regenerates.</summary>
    [RelayCommand]
    private Task EditAndResend() =>
        _editResend is null || string.IsNullOrWhiteSpace(EditedText)
            ? Task.CompletedTask
            : _editResend(EditedText);

    /// <summary>Promotes the turn to an artifact, handing the built request to the consumer (M3).</summary>
    [RelayCommand]
    private Task Promote()
    {
        if (_promote is null || _buildPromoteRequest is null)
            return Task.CompletedTask;
        return _promote(_buildPromoteRequest());
    }

    /// <summary>Deletes the turn from the conversation.</summary>
    [RelayCommand]
    private Task Delete() => _delete?.Invoke() ?? Task.CompletedTask;
}
