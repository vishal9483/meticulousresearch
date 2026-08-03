namespace MeticulousResearch.Core.Projects;

/// <summary>
/// Aggregated project-dashboard figures (SPEC §3.1): the counts of a project's child entities
/// and the most recent activity instant across the project and its children. The consolidated
/// cost panel is added later by <c>cost-tracking</c>; this record intentionally leaves that slot
/// to that feature.
/// </summary>
/// <param name="ProjectId">The project these figures describe.</param>
/// <param name="ResourceCount">Number of resources attached to the project.</param>
/// <param name="ConversationCount">Number of conversations in the project.</param>
/// <param name="ArtifactCount">Number of artifacts in the project.</param>
/// <param name="LastActivity">
/// The most recent activity timestamp across the project and its children, or <c>null</c> when
/// the project has no recorded activity.
/// </param>
public sealed record ProjectDashboard(
    string ProjectId,
    int ResourceCount,
    int ConversationCount,
    int ArtifactCount,
    DateTimeOffset? LastActivity);
