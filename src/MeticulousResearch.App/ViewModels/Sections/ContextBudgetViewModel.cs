using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.Core.Budget;
using MeticulousResearch.Core.Resources;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// Composer budget meter (SPEC §3.2, §8): a live, window-free view-model that estimates the tokens
/// of the project's enabled resources plus a fixed overhead against the selected model's context
/// window and the configured budget, exposes a warning state, and offers the two resolutions —
/// deselect a resource or switch to a larger-window model — recomputing reactively after each. The
/// numbers are labeled "estimated" (authoritative counts come from usage post-send, SPEC §3.6).
/// Content is never silently truncated: while the estimate is over the model window
/// <see cref="CanGenerate"/> is <c>false</c> and <see cref="AttemptGeneration"/> refuses without
/// dropping or truncating any resource.
/// </summary>
public sealed partial class ContextBudgetViewModel : ViewModelBase
{
    private readonly string _projectId;
    private readonly IResourceService _resources;
    private readonly IContextBudgetService _budget;
    private readonly ContextBudgetScope _scope;
    private ModelWindow _model;

    /// <summary>
    /// Creates the budget meter for <paramref name="projectId"/> against an initial
    /// <paramref name="model"/> window and a fixed instruction/message <paramref name="scope"/>
    /// overhead, and computes the first estimate.
    /// </summary>
    public ContextBudgetViewModel(
        string projectId,
        IResourceService resources,
        IContextBudgetService budget,
        ModelWindow model,
        ContextBudgetScope scope)
    {
        _projectId = projectId;
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        _budget = budget ?? throw new ArgumentNullException(nameof(budget));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Contributors = new ObservableCollection<ResourceContribution>();
        Recompute();
    }

    /// <summary>The most recently computed estimate breakdown, thresholds, and status.</summary>
    public ContextBudgetEstimate Estimate { get; private set; } = null!;

    /// <summary>The enabled resources ordered largest-first (the deselect guidance).</summary>
    public ObservableCollection<ResourceContribution> Contributors { get; }

    /// <summary>The estimated total tokens (enabled resources + overhead).</summary>
    public long EstimatedTotal => Estimate.TotalTokens;

    /// <summary>The selected model's context window (hard ceiling).</summary>
    public long WindowTokens => Estimate.WindowTokens;

    /// <summary>The configured context budget (soft threshold).</summary>
    public long BudgetTokens => Estimate.BudgetTokens;

    /// <summary>Whether the meter is in a warning state (over budget or over window).</summary>
    public bool HasWarning => Estimate.HasWarning;

    /// <summary>The warning message matching the current status.</summary>
    public string WarningMessage => Estimate.WarningMessage;

    /// <summary>The "estimated" label these numbers are shown under.</summary>
    public string Label => Estimate.Label;

    /// <summary>The current status (ok / over budget / over window).</summary>
    public ContextBudgetStatus Status => Estimate.Status;

    /// <summary>
    /// Whether generation may proceed. False while over the model window: the user must first
    /// deselect resources or switch model — the app never truncates to fit.
    /// </summary>
    public bool CanGenerate => Status != ContextBudgetStatus.OverWindow;

    /// <summary>
    /// Attempts to begin a generation. Returns <c>true</c> only when the estimate is within the
    /// model window; when over the window it returns <c>false</c> without dropping or truncating any
    /// resource (SPEC §3.2 — no silent truncation). Resolve the overage via
    /// <see cref="DeselectCommand"/> or <see cref="SwitchModel"/> first.
    /// </summary>
    public bool AttemptGeneration() => CanGenerate;

    /// <summary>
    /// Deselects (disables) a resource so it leaves the enabled scope, then recomputes the estimate
    /// live. This is the user's way to bring an over-budget total back under threshold.
    /// </summary>
    [RelayCommand]
    public void Deselect(string resourceId)
    {
        _resources.SetEnabled(resourceId, false);
        Recompute();
    }

    /// <summary>
    /// Switches the selected model, re-resolving the context window from the new model and
    /// recomputing the estimate (e.g. moving to a larger-window model clears an over-window warning).
    /// </summary>
    public void SwitchModel(ModelWindow model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        Recompute();
    }

    private void Recompute()
    {
        Estimate = _budget.Estimate(_projectId, _scope, _model);

        Contributors.Clear();
        foreach (var c in Estimate.LargestContributors)
            Contributors.Add(c);

        OnPropertyChanged(nameof(Estimate));
        OnPropertyChanged(nameof(EstimatedTotal));
        OnPropertyChanged(nameof(WindowTokens));
        OnPropertyChanged(nameof(BudgetTokens));
        OnPropertyChanged(nameof(HasWarning));
        OnPropertyChanged(nameof(WarningMessage));
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(CanGenerate));
    }
}
