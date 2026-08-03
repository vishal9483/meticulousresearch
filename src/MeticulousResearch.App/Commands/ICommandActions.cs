namespace MeticulousResearch.App.Commands;

/// <summary>
/// The application actions the core palette commands delegate to (SPEC §3.5). The palette
/// <em>invokes</em> these — it never reimplements the underlying create/search flows. Downstream
/// features supply the concrete wiring to projects-crud / conversations / artifact-creation /
/// full-text-search.
/// </summary>
public interface ICommandActions
{
    /// <summary>Starts the "create a new project" flow.</summary>
    void NewProject();

    /// <summary>Starts the "create a new conversation" flow.</summary>
    void NewConversation();

    /// <summary>Starts the "create a new artifact" flow.</summary>
    void NewArtifact();

    /// <summary>Opens the search experience.</summary>
    void OpenSearch();
}
