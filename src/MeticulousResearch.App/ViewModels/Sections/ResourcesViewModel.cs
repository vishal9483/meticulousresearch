using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Resources.Extraction;
using MeticulousResearch.Core.Resources.Url;
using MeticulousResearch.Core.Search;

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
    private readonly ISearchService? _search;

    /// <summary>Designed empty-state message shown when the project has no resources yet.</summary>
    public const string EmptyStateMessage = "No resources yet. Add pasted text to ground Claude in your notes.";

    /// <summary>Designed empty-state message shown when a search matches no resources.</summary>
    public const string NoSearchMatchesMessage = "No resources match your search.";

    /// <summary>
    /// Design-time / window-free constructor without a service (renders the designed empty state).
    /// </summary>
    public ResourcesViewModel(string projectId) : this(projectId, null, null) { }

    /// <summary>
    /// Creates the Resources section for <paramref name="projectId"/>, wired to the resource
    /// service so pasted text can be added, listed, and previewed. When no service is supplied the
    /// section renders its designed empty state.
    /// </summary>
    public ResourcesViewModel(string projectId, IResourceService? resources)
        : this(projectId, resources, null) { }

    /// <summary>
    /// Creates the Resources section wired to the resource service and the full-text
    /// <paramref name="search"/> service that backs the live search box (full-text-search/phase.md).
    /// </summary>
    public ResourcesViewModel(string projectId, IResourceService? resources, ISearchService? search) : base(projectId)
    {
        _resources = resources;
        _search = search;
        Resources = new ObservableCollection<ResourceRowViewModel>();
        Resources.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(VisibleResources));
            OnPropertyChanged(nameof(HasNoSearchMatches));
        };
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

    private string _searchQuery = "";

    /// <summary>
    /// The text typed into the resources search box. Filtering is live: setting it recomputes
    /// <see cref="VisibleResources"/> against the project's full-text index and refreshes the
    /// no-matches empty state (full-text-search/phase.md).
    /// </summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value ?? ""))
            {
                OnPropertyChanged(nameof(VisibleResources));
                OnPropertyChanged(nameof(HasActiveSearch));
                OnPropertyChanged(nameof(HasNoSearchMatches));
            }
        }
    }

    /// <summary>Whether a non-empty search query is currently narrowing the list.</summary>
    public bool HasActiveSearch => !string.IsNullOrWhiteSpace(_searchQuery);

    /// <summary>
    /// The resources currently visible in the table: all resources when the search box is empty,
    /// otherwise only those whose extracted text or title match the query (project-scoped,
    /// relevance-ranked via the full-text index), preserving the search's ranking order.
    /// </summary>
    public IReadOnlyList<ResourceRowViewModel> VisibleResources
    {
        get
        {
            if (!HasActiveSearch)
                return Resources.ToList();

            if (_search is not null)
            {
                var byId = Resources.ToDictionary(r => r.Id);
                return _search.SearchResources(ProjectId, _searchQuery)
                    .Where(hit => byId.ContainsKey(hit.Id))
                    .Select(hit => byId[hit.Id])
                    .ToList();
            }

            return Resources
                .Where(r => r.Title.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    /// <summary>
    /// Whether an active search matched nothing (drives the designed "no matches" empty state).
    /// </summary>
    public bool HasNoSearchMatches => HasActiveSearch && VisibleResources.Count == 0;

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
                OnPropertyChanged(nameof(SelectedMetadata));
                OnPropertyChanged(nameof(CanReExtractSelected));
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
        var row = CreateRow(resource);
        Resources.Insert(0, row);
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EnabledTokenTotal));
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

                var row = CreateRow(result.Resource);
                Resources.Insert(0, row);
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(EnabledTokenTotal));
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

            var row = CreateRow(resource);
            Resources.Insert(0, row);
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(EnabledTokenTotal));
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
                Resources.Add(CreateRow(r));
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EnabledTokenTotal));
    }

    /// <summary>
    /// Wraps a persisted resource in a display row wired so flipping its enabled toggle persists the
    /// new scope through the service and recomputes the enabled-scope token total.
    /// </summary>
    private ResourceRowViewModel CreateRow(Core.Data.Entities.Resource resource)
    {
        var row = new ResourceRowViewModel(resource);
        row.EnabledChanged += enabled =>
        {
            _resources?.SetEnabled(row.Id, enabled);
            OnPropertyChanged(nameof(EnabledTokenTotal));
        };
        return row;
    }

    /// <summary>
    /// The total token estimate across the project's <em>enabled</em> resources (SPEC §3.2) — the
    /// pre-send generation scope. Disabled resources are excluded. Reused by <c>context-budget</c>.
    /// </summary>
    public long EnabledTokenTotal => Resources.Where(r => r.Enabled).Sum(r => r.TokenEstimate);

    /// <summary>
    /// A one-line metadata summary of the selected resource for the preview pane — its type, byte
    /// size, and token estimate; empty when nothing is selected.
    /// </summary>
    public string SelectedMetadata => _selectedResource is null
        ? ""
        : $"{_selectedResource.TypeDisplay} \u00b7 {_selectedResource.ByteSize} bytes \u00b7 {_selectedResource.TokenEstimate} tokens";

    /// <summary>Whether the selected resource offers a re-extract action (not pasted text).</summary>
    public bool CanReExtractSelected => _selectedResource?.CanReExtract == true;

    /// <summary>
    /// Renames the selected resource to <paramref name="newTitle"/>. A blank title is rejected with an
    /// inline <see cref="RenameError"/> and the title is left unchanged; otherwise the row's title
    /// updates in place.
    /// </summary>
    [RelayCommand]
    public void RenameSelected(string? newTitle)
    {
        RenameError = null;
        if (_selectedResource is null || _resources is null)
            return;

        if (string.IsNullOrWhiteSpace(newTitle))
        {
            RenameError = "A resource title must not be blank.";
            return;
        }

        var updated = _resources.Rename(_selectedResource.Id, newTitle);
        _selectedResource.Title = updated.Title;
    }

    private string? _renameError;

    /// <summary>Inline validation error surfaced when a rename is rejected; null when valid.</summary>
    public string? RenameError
    {
        get => _renameError;
        private set
        {
            if (SetProperty(ref _renameError, value))
                OnPropertyChanged(nameof(HasRenameError));
        }
    }

    /// <summary>Whether an inline rename validation error is currently shown.</summary>
    public bool HasRenameError => !string.IsNullOrEmpty(_renameError);

    /// <summary>
    /// Re-extracts the selected resource against its stored original, refreshing the preview text and
    /// its token-estimate contribution (and the enabled-scope total). Unavailable for pasted text.
    /// </summary>
    [RelayCommand]
    public void ReExtractSelected()
    {
        if (_selectedResource is null || _resources is null || !_selectedResource.CanReExtract)
            return;

        var result = _resources.ReExtract(_selectedResource.Id);
        _selectedResource.TokenEstimate = result.Resource.TokenEstimate ?? 0;
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(SelectedMetadata));
        OnPropertyChanged(nameof(EnabledTokenTotal));
    }

    private bool _isConfirmingRemove;

    /// <summary>
    /// Whether the UI is asking the analyst to confirm removal of the selected resource. Nothing is
    /// deleted until <see cref="ConfirmRemoveCommand"/> runs.
    /// </summary>
    public bool IsConfirmingRemove
    {
        get => _isConfirmingRemove;
        private set => SetProperty(ref _isConfirmingRemove, value);
    }

    /// <summary>
    /// Begins removal of the selected resource by asking for confirmation first; no resource is
    /// deleted at this point (SPEC §3.2 — remove is confirmed).
    /// </summary>
    [RelayCommand]
    public void RemoveSelected()
    {
        if (_selectedResource is null)
            return;
        IsConfirmingRemove = true;
    }

    /// <summary>Cancels a pending remove confirmation without deleting anything.</summary>
    [RelayCommand]
    public void CancelRemove() => IsConfirmingRemove = false;

    /// <summary>
    /// Confirms and performs removal of the selected resource: deletes its row and on-disk files
    /// through the service, drops the row from the table, clears the selection, and refreshes totals.
    /// </summary>
    [RelayCommand]
    public void ConfirmRemove()
    {
        if (_selectedResource is null || _resources is null)
        {
            IsConfirmingRemove = false;
            return;
        }

        _resources.Remove(_selectedResource.Id);
        Resources.Remove(_selectedResource);
        SelectedResource = null;
        IsConfirmingRemove = false;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EnabledTokenTotal));
    }
}
