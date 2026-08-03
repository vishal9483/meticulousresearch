using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MeticulousResearch.Core.Cost;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// The consolidated project-cost panel view-model (SPEC §3.6, §9.1(7)) shown on the project
/// dashboard. Reads <see cref="ICostService"/> and surfaces the total spend plus the three
/// breakdowns — by source (conversations vs artifacts), by model, and by time window (today / this
/// week / all-time). All figures are recomputed from stored tokens at current prices, so the panel
/// re-loads to reflect a price change with no token mutation.
/// </summary>
public sealed partial class ConsolidatedCostViewModel : ViewModelBase
{
    private readonly ICostService _cost;
    private readonly string _projectId;

    /// <summary>Creates the panel view-model for <paramref name="projectId"/> and loads its figures.</summary>
    /// <param name="cost">The cost engine to read from.</param>
    /// <param name="projectId">The project whose spend is shown.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public ConsolidatedCostViewModel(ICostService cost, string projectId)
    {
        _cost = cost ?? throw new ArgumentNullException(nameof(cost));
        _projectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
        ByModel = new ObservableCollection<ModelSpend>();
        Load();
    }

    /// <summary>Total (all-time) project spend in USD.</summary>
    [ObservableProperty]
    private decimal _total;

    /// <summary>All-time spend attributed to conversation turns, in USD.</summary>
    [ObservableProperty]
    private decimal _conversations;

    /// <summary>All-time spend attributed to artifact-version generations, in USD.</summary>
    [ObservableProperty]
    private decimal _artifacts;

    /// <summary>Today's spend in USD (bucketed against the injected clock).</summary>
    [ObservableProperty]
    private decimal _today;

    /// <summary>This week's (rolling 7-day) spend in USD.</summary>
    [ObservableProperty]
    private decimal _week;

    /// <summary>All-time spend in USD.</summary>
    [ObservableProperty]
    private decimal _allTime;

    /// <summary>Per-model spend for the by-model breakdown.</summary>
    public ObservableCollection<ModelSpend> ByModel { get; }

    /// <summary>The total spend formatted for display (USD, 2 decimal places).</summary>
    public string TotalDisplay => Money(Total);

    /// <summary>The conversations bucket formatted for display.</summary>
    public string ConversationsDisplay => Money(Conversations);

    /// <summary>The artifacts bucket formatted for display.</summary>
    public string ArtifactsDisplay => Money(Artifacts);

    /// <summary>(Re)loads all figures from the cost engine at current prices.</summary>
    public void Load()
    {
        var all = _cost.GetProjectCost(_projectId, CostWindow.AllTime);
        Total = all.Total;
        Conversations = all.Conversations;
        Artifacts = all.Artifacts;
        AllTime = all.Total;
        Today = _cost.GetProjectCost(_projectId, CostWindow.Today).Total;
        Week = _cost.GetProjectCost(_projectId, CostWindow.Week).Total;

        ByModel.Clear();
        foreach (var kv in all.ByModel.OrderByDescending(kv => kv.Value))
            ByModel.Add(new ModelSpend(kv.Key, kv.Value));

        OnPropertyChanged(nameof(TotalDisplay));
        OnPropertyChanged(nameof(ConversationsDisplay));
        OnPropertyChanged(nameof(ArtifactsDisplay));
    }

    private static string Money(decimal value)
        => value.ToString("C2", CultureInfo.GetCultureInfo("en-US"));

    /// <summary>One model's spend row in the by-model breakdown.</summary>
    /// <param name="Model">The model id.</param>
    /// <param name="Cost">The USD spend attributed to it.</param>
    public sealed record ModelSpend(string Model, decimal Cost)
    {
        /// <summary>The spend formatted for display (USD, 2 decimal places).</summary>
        public string CostDisplay => Cost.ToString("C2", CultureInfo.GetCultureInfo("en-US"));
    }
}
