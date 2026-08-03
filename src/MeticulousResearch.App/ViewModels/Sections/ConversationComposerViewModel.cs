using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Models;
using MeticulousResearch.Core.Resources;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// The conversation composer (image-attachments, SPEC §3.2.1): holds the draft text and the
/// zero-or-more per-turn image attachments the analyst pastes or attaches, renders them as inline
/// thumbnails, exposes a pre-send token estimate that includes an "estimated" image contribution,
/// and — via the shared model picker — warns and offers a switch when the selected model cannot read
/// images. Attachments are per-turn message content, never project resources. Window-free so its
/// logic is <c>@unit</c>-testable.
/// </summary>
public sealed partial class ConversationComposerViewModel : ObservableObject
{
    /// <summary>The default file name given to a pasted image (which carries no name of its own).</summary>
    public const string PastedImageFileName = "pasted-image.png";

    private readonly PendingTurnTokenEstimator _estimator;
    private readonly ObservableCollection<ImageAttachment> _attachments = new();

    /// <summary>Builds a composer over the model picker and a token estimator for pre-send estimates.</summary>
    /// <param name="modelPicker">The shared model picker (model-selector), drives the vision warning.</param>
    /// <param name="tokenEstimator">The token estimator used for the pre-send estimate.</param>
    public ConversationComposerViewModel(
        ModelPickerViewModel modelPicker,
        ITokenEstimator tokenEstimator)
    {
        ModelPicker = modelPicker ?? throw new ArgumentNullException(nameof(modelPicker));
        _estimator = new PendingTurnTokenEstimator(
            tokenEstimator ?? throw new ArgumentNullException(nameof(tokenEstimator)));
        Attachments = new ReadOnlyObservableCollection<ImageAttachment>(_attachments);
    }

    /// <summary>The model picker whose selected model drives the vision warning (§3.2.1 / §6.3).</summary>
    public ModelPickerViewModel ModelPicker { get; }

    /// <summary>The composed text of the pending turn.</summary>
    [ObservableProperty]
    private string _text = "";

    /// <summary>The pending turn's image attachments, in attach order (rendered as thumbnails).</summary>
    public ReadOnlyObservableCollection<ImageAttachment> Attachments { get; }

    /// <summary>Whether at least one image is attached to the pending turn.</summary>
    public bool HasAttachments => _attachments.Count > 0;

    /// <summary>
    /// Pastes an image into the composer, attaching it to the pending turn. Pasted images carry no
    /// file name, so <see cref="PastedImageFileName"/> is used unless one is supplied.
    /// </summary>
    /// <param name="bytes">The pasted image bytes.</param>
    /// <param name="fileName">An optional file name for the pasted image.</param>
    public void PasteImage(byte[] bytes, string? fileName = null)
        => AddAttachment(ImageAttachment.FromBytes(
            string.IsNullOrWhiteSpace(fileName) ? PastedImageFileName : fileName!, bytes));

    /// <summary>Attaches an image file (by name + bytes) to the pending turn.</summary>
    /// <param name="fileName">The image file name (e.g. <c>chart.png</c>).</param>
    /// <param name="bytes">The image bytes.</param>
    public void AttachImage(string fileName, byte[] bytes)
        => AddAttachment(ImageAttachment.FromBytes(fileName, bytes));

    /// <summary>Attaches an image file from disk to the pending turn.</summary>
    /// <param name="path">Absolute path to a supported image file.</param>
    public void AttachImageFile(string path) => AddAttachment(ImageAttachment.FromFile(path));

    /// <summary>Removes a pending attachment before the turn is sent.</summary>
    [RelayCommand]
    public void RemoveAttachment(ImageAttachment attachment)
    {
        if (attachment is null)
            return;
        if (_attachments.Remove(attachment))
            OnAttachmentsChanged();
    }

    /// <summary>The pending turn's attachments as a list, for sending.</summary>
    public IReadOnlyList<ImageAttachment> PendingAttachments => _attachments.ToList();

    /// <summary>
    /// The pre-send token estimate for the current draft + attachments (labeled "estimated"). Its
    /// image contribution is positive whenever an image is attached.
    /// </summary>
    public PendingTurnEstimate Estimate => _estimator.Estimate(Text, _attachments);

    partial void OnTextChanged(string value) => OnPropertyChanged(nameof(Estimate));

    private void AddAttachment(ImageAttachment attachment)
    {
        _attachments.Add(attachment);
        OnAttachmentsChanged();
    }

    private void OnAttachmentsChanged()
    {
        ModelPicker.ImageInScope = HasAttachments;
        OnPropertyChanged(nameof(HasAttachments));
        OnPropertyChanged(nameof(PendingAttachments));
        OnPropertyChanged(nameof(Estimate));
    }
}
