using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.Core.Models;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// The tiered model picker (model-selector/phase.md, SPEC §6, §3.3): shows the friendly tiers plus an
/// "All models" list, tracks the currently selected model, supports a per-turn override without
/// mutating the conversation default, and surfaces the vision warning + "switch model" action when an
/// image is in scope and the chosen model lacks vision (§3.2.1). Window-free so its logic is
/// <c>@unit</c>-testable.
/// </summary>
public sealed partial class ModelPickerViewModel : ObservableObject
{
    private readonly IModelCatalog _catalog;
    private readonly ModelSelection _selection;

    /// <summary>Builds the picker over a catalog, defaulting the selection to the catalog default model.</summary>
    /// <param name="catalog">The model catalog (owned by <c>model-selector</c>).</param>
    public ModelPickerViewModel(IModelCatalog catalog)
        : this(catalog, catalog is null ? throw new ArgumentNullException(nameof(catalog)) : catalog.DefaultModelId)
    {
    }

    /// <summary>Builds the picker over a catalog with an explicit initial conversation model.</summary>
    /// <param name="catalog">The model catalog.</param>
    /// <param name="initialModelId">The conversation's initial model id (e.g. the project default).</param>
    public ModelPickerViewModel(IModelCatalog catalog, string initialModelId)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _selection = new ModelSelection(initialModelId);
        Tiers = new ReadOnlyCollection<ModelInfo>(_catalog.Tiers.ToList());
        AdditionalModels = new ReadOnlyCollection<ModelInfo>(_catalog.AdditionalModels.ToList());
    }

    /// <summary>The friendly tiers shown at the top of the picker.</summary>
    public IReadOnlyList<ModelInfo> Tiers { get; }

    /// <summary>The additional (non-tier) models shown under the "All models" section.</summary>
    public IReadOnlyList<ModelInfo> AdditionalModels { get; }

    /// <summary>The conversation's currently selected model id (applies to subsequent turns).</summary>
    public string CurrentModelId => _selection.ConversationModelId;

    /// <summary>A friendly label for the current model (its catalog name, or the raw id).</summary>
    public string CurrentModelLabel => _catalog.TryGet(CurrentModelId)?.Name ?? CurrentModelId;

    /// <summary>Whether an image is attached to the turn (drives the vision warning, §3.2.1).</summary>
    [ObservableProperty]
    private bool _imageInScope;

    /// <summary>The active vision warning, or <c>null</c> when the selection is fine.</summary>
    [ObservableProperty]
    private VisionWarning? _visionWarning;

    /// <summary>Whether a vision warning is currently shown.</summary>
    public bool HasVisionWarning => VisionWarning is not null;

    /// <summary>
    /// Selects a model (by tier name or id) as the conversation model, then recomputes the vision
    /// warning. Selection is advisory-safe: a non-vision model with an image in scope still selects,
    /// but raises a warning offering a switch.
    /// </summary>
    /// <param name="tierOrId">A friendly tier name or a concrete model id.</param>
    [RelayCommand]
    public void SelectModel(string tierOrId)
    {
        var resolved = _catalog.Resolve(tierOrId);
        var modelId = resolved?.Id ?? tierOrId;
        _selection.SetConversationModel(modelId);
        OnPropertyChanged(nameof(CurrentModelId));
        OnPropertyChanged(nameof(CurrentModelLabel));
        RefreshVisionWarning();
    }

    /// <summary>Resolves the model id to send for a turn, honoring an optional per-message override.</summary>
    /// <param name="perMessageOverride">An optional one-turn override (tier name or id).</param>
    public string ResolveForTurn(string? perMessageOverride = null)
    {
        if (string.IsNullOrWhiteSpace(perMessageOverride))
            return _selection.ResolveForTurn();

        var resolved = _catalog.Resolve(perMessageOverride);
        return _selection.ResolveForTurn(resolved?.Id ?? perMessageOverride);
    }

    /// <summary>Switches the selection to the vision-capable model suggested by the active warning.</summary>
    [RelayCommand(CanExecute = nameof(CanSwitchToVisionModel))]
    public void SwitchToVisionModel()
    {
        var target = VisionWarning?.SuggestedVisionModelId;
        if (!string.IsNullOrWhiteSpace(target))
            SelectModel(target!);
    }

    /// <summary>Whether a vision-capable switch target is currently offered.</summary>
    public bool CanSwitchToVisionModel => !string.IsNullOrWhiteSpace(VisionWarning?.SuggestedVisionModelId);

    partial void OnImageInScopeChanged(bool value) => RefreshVisionWarning();

    partial void OnVisionWarningChanged(VisionWarning? value)
    {
        OnPropertyChanged(nameof(HasVisionWarning));
        OnPropertyChanged(nameof(CanSwitchToVisionModel));
        SwitchToVisionModelCommand.NotifyCanExecuteChanged();
    }

    private void RefreshVisionWarning()
        => VisionWarning = ModelVisionAdvisor.Advise(_catalog, CurrentModelId, ImageInScope);
}
