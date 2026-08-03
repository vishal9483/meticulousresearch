using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Resources.Extraction;
using MeticulousResearch.Core.Resources.Url;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// Resources section — the project's source material (text/file/URL/image) that Claude grounds its
/// answers in (SPEC §3.2). This slice implements the text-paste flow: an "Add resource → Paste
/// text" entry, a table of resources (title/type/size/tokens/enabled), and a preview pane showing
/// the selected resource's extracted text. Window-free so the flow is <c>@unit</c>-testable; the
/// designed empty state renders when a project has no resources.
/// </summary>
public sealed partial class ResourcesViewModel : SectionViewModel
{
    private readonly IResourceService? _resources;

    /// <summary>Designed empty-state message shown when the project has no resources yet.</summary>
    public const string EmptyStateMessage = "No resources yet. Add pasted text to ground Claude in your notes.";

    /// <summary>
    /// Design-time / window-free constructor without a service (renders the designed empty state).
    /// </summary>
    public ResourcesViewModel(string projectId) : this(projectId, null) { }

    /// <summary>
    /// Creates the Resources section for <paramref name="projectId"/>, wired to the resource
    /// service so pasted text can be added, listed, and previewed. When no service is supplied the
    /// section renders its designed empty state.
    /// </summary>
    public ResourcesViewModel(string projectId, IResourceService? resources) : base(projectId)
    {
        _resources = resources;
        Resources = new ObservableCollection<ResourceRowViewModel>();
        Load();
    }

    /// <inheritdoc />
    public override NavigationSection Section => NavigationSection.Resources;

    /// <inheritdoc />
    public override string Title => "Resources";

    /// <summary>Designed one-line description of what this section is for.</summary>
    public string Headline => "Source material Claude grounds its answers in.";

    /// <summary>The resources in this project, most recently added first.</summary>
    public ObservableCollection<ResourceRowViewModel> Resources { get; }

    /// <summary>Whether the project currently has no resources (drives the empty state).</summary>
    public bool IsEmpty => Resources.Count == 0;

    private ResourceRowViewModel? _selectedResource;

    /// <summary>The row selected in the table; drives the preview pane.</summary>
    public ResourceRowViewModel? SelectedResource
    {
        get => _selectedResource;
        set
        {
            if (SetProperty(ref _selectedResource, value))
            {
                OnPropertyChanged(nameof(PreviewText));
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(SelectedSourceUri));
                OnPropertyChanged(nameof(HasSelectedSourceUri));
            }
        }
    }

    /// <summary>Whether a resource is selected (drives the preview pane's visibility).</summary>
    public bool HasSelection => _selectedResource is not null;

    /// <summary>
    /// The selected resource's retained source URL/path shown in the preview pane for provenance
    /// (e.g. the original URL of a URL resource); empty when none.
    /// </summary>
    public string SelectedSourceUri => _selectedResource?.SourceUri ?? "";

    /// <summary>Whether the selected resource has a source URL to show for provenance.</summary>
    public bool HasSelectedSourceUri => !string.IsNullOrEmpty(_selectedResource?.SourceUri);

    /// <summary>The extracted text of the selected resource, shown in the preview pane.</summary>
    public string PreviewText =>
        _selectedResource is null || _resources is null
            ? ""
            : _resources.GetExtractedText(_selectedResource.Id);

    private string _draftTitle = "";

    /// <summary>The title typed into the "Add resource → Paste text" entry.</summary>
    public string DraftTitle
    {
        get => _draftTitle;
        set => SetProperty(ref _draftTitle, value);
    }

    private string _draftText = "";

    /// <summary>The text typed/pasted into the "Add resource → Paste text" entry.</summary>
    public string DraftText
    {
        get => _draftText;
        set => SetProperty(ref _draftText, value);
    }

    private string? _validationError;

    /// <summary>Inline validation error surfaced when a paste is rejected; null when valid.</summary>
    public string? ValidationError
    {
        get => _validationError;
        private set
        {
            if (SetProperty(ref _validationError, value))
                OnPropertyChanged(nameof(HasValidationError));
        }
    }

    /// <summary>Whether an inline validation error is currently shown.</summary>
    public bool HasValidationError => !string.IsNullOrEmpty(_validationError);

    /// <summary>
    /// Adds a pasted-text resource from <see cref="DraftText"/>/<see cref="DraftTitle"/>. Empty or
    /// whitespace-only text is rejected with an inline <see cref="ValidationError"/> and no resource
    /// is created; on success the new row is inserted at the top and selected, and the draft clears.
    /// </summary>
    [RelayCommand]
    public void AddPastedText()
    {
        if (string.IsNullOrWhiteSpace(DraftText))
        {
            ValidationError = "Enter some text to add as a resource.";
            return;
        }

        ValidationError = null;

        if (_resources is null)
            return;

        var resource = _resources.AddText(ProjectId, DraftTitle, DraftText);
        var row = new ResourceRowViewModel(resource);
        Resources.Insert(0, row);
        OnPropertyChanged(nameof(IsEmpty));
        SelectedResource = row;
        DraftTitle = "";
        DraftText = "";
    }

    private bool _isExtracting;

    /// <summary>
    /// Whether a file upload/extraction is currently running (drives the async progress indicator).
    /// </summary>
    public bool IsExtracting
    {
        get => _isExtracting;
        private set => SetProperty(ref _isExtracting, value);
    }

    private string? _uploadHint;

    /// <summary>
    /// A hint surfaced after an upload whose extraction produced no text (e.g. a scanned PDF should
    /// be added as an image resource), or a rejection/failure message. Null when there is none.
    /// </summary>
    public string? UploadHint
    {
        get => _uploadHint;
        private set
        {
            if (SetProperty(ref _uploadHint, value))
                OnPropertyChanged(nameof(HasUploadHint));
        }
    }

    /// <summary>Whether an upload hint is currently shown.</summary>
    public bool HasUploadHint => !string.IsNullOrEmpty(_uploadHint);

    /// <summary>
    /// Uploads one or more files as resources (SPEC §3.2). Extraction runs off the UI thread with
    /// <see cref="IsExtracting"/> driving a progress indicator; each new resource is inserted at the
    /// top and selected so its extracted preview shows. Unsupported types are rejected with a
    /// message and no resource; extraction that yields no text surfaces the returned hint. The
    /// original blob is always stored so nothing is lost on failure.
    /// </summary>
    [RelayCommand]
    public async Task UploadFilesAsync(IReadOnlyList<string>? filePaths)
    {
        if (_resources is null || filePaths is null || filePaths.Count == 0)
            return;

        UploadHint = null;
        IsExtracting = true;
        try
        {
            foreach (var path in filePaths)
            {
                FileExtractionResult result;
                try
                {
                    result = await Task.Run(() => _resources.AddFile(ProjectId, path)).ConfigureAwait(true);
                }
                catch (UnsupportedFileTypeException ex)
                {
                    UploadHint = ex.Message;
                    continue;
                }

                var row = new ResourceRowViewModel(result.Resource);
                Resources.Insert(0, row);
                OnPropertyChanged(nameof(IsEmpty));
                SelectedResource = row;

                if (result.Status == ExtractionStatus.Failed)
                    UploadHint = $"Extraction failed: {result.FailureReason} You can re-extract from the resource's actions.";
                else if (result.Status == ExtractionStatus.Empty && !string.IsNullOrEmpty(result.Hint))
                    UploadHint = result.Hint;
            }
        }
        finally
        {
            IsExtracting = false;
        }
    }

    private string _draftUrl = "";

    /// <summary>The URL typed into the "Add resource → URL" entry.</summary>
    public string DraftUrl
    {
        get => _draftUrl;
        set => SetProperty(ref _draftUrl, value);
    }

    private string? _urlValidationError;

    /// <summary>Inline validation error surfaced when a malformed URL is entered; null when valid.</summary>
    public string? UrlValidationError
    {
        get => _urlValidationError;
        private set
        {
            if (SetProperty(ref _urlValidationError, value))
                OnPropertyChanged(nameof(HasUrlValidationError));
        }
    }

    /// <summary>Whether an inline URL validation error is currently shown.</summary>
    public bool HasUrlValidationError => !string.IsNullOrEmpty(_urlValidationError);

    private string? _urlError;

    /// <summary>A human-readable fetch/empty-content error surfaced after an add attempt; null when none.</summary>
    public string? UrlError
    {
        get => _urlError;
        private set
        {
            if (SetProperty(ref _urlError, value))
                OnPropertyChanged(nameof(HasUrlError));
        }
    }

    /// <summary>Whether a URL fetch error is currently shown.</summary>
    public bool HasUrlError => !string.IsNullOrEmpty(_urlError);

    private bool _isFetching;

    /// <summary>Whether a URL fetch/convert is currently running (drives the fetching indicator).</summary>
    public bool IsFetching
    {
        get => _isFetching;
        private set => SetProperty(ref _isFetching, value);
    }

    /// <summary>
    /// Adds a URL resource from <see cref="DraftUrl"/> (SPEC §3.2). A malformed URL is rejected with
    /// an inline <see cref="UrlValidationError"/> and no resource is created. Otherwise the page is
    /// fetched and converted off the UI thread with <see cref="IsFetching"/> driving a progress
    /// indicator; a fetch failure or empty-content page surfaces a human-readable
    /// <see cref="UrlError"/> and creates no resource, while success inserts the new row at the top,
    /// selects it so its converted preview and retained source URL show, and clears the draft.
    /// </summary>
    [RelayCommand]
    public async Task AddUrlAsync()
    {
        UrlValidationError = null;
        UrlError = null;

        if (string.IsNullOrWhiteSpace(DraftUrl))
        {
            UrlValidationError = "Enter a URL to add.";
            return;
        }

        if (_resources is null)
            return;

        var url = DraftUrl;
        IsFetching = true;
        try
        {
            Core.Data.Entities.Resource resource;
            try
            {
                resource = await Task.Run(() => _resources.AddUrl(ProjectId, url)).ConfigureAwait(true);
            }
            catch (ArgumentException)
            {
                UrlValidationError = "That doesn't look like a valid URL. Enter a full http(s) address.";
                return;
            }
            catch (UrlResourceException ex)
            {
                UrlError = ex.Message;
                return;
            }

            var row = new ResourceRowViewModel(resource);
            Resources.Insert(0, row);
            OnPropertyChanged(nameof(IsEmpty));
            SelectedResource = row;
            DraftUrl = "";
        }
        finally
        {
            IsFetching = false;
        }
    }

    /// <summary>(Re)loads the resource table from the service.</summary>
    public void Load()
    {
        Resources.Clear();
        if (_resources is not null)
        {
            foreach (var r in _resources.List(ProjectId))
                Resources.Add(new ResourceRowViewModel(r));
        }

        OnPropertyChanged(nameof(IsEmpty));
    }
}
