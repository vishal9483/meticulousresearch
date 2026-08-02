using CommunityToolkit.Mvvm.ComponentModel;

namespace MeticulousResearch.App.ViewModels;

/// <summary>
/// Base type for every view-model in the shell. Derives from CommunityToolkit's
/// <see cref="ObservableObject"/> so change notification and source-generated
/// <c>[ObservableProperty]</c>/<c>[RelayCommand]</c> members are available. The MVVM
/// view-location layer (DataTemplates keyed by VM type) maps each VM to its view.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
}
