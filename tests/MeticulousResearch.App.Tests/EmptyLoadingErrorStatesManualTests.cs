namespace MeticulousResearch.App.Tests;

/// <summary>
/// @manual checklist scenarios from docs/features/empty-loading-error-states/tests.md. These are
/// visual/consistency passes performed by a human during PR review (SPEC §3.7, §9.1(10)); they are
/// tagged <c>Category=manual</c> and skipped in the automated gate.
/// </summary>
public class EmptyLoadingErrorStatesManualTests
{
    // Scenario: Skeleton loaders match the shape of the content they precede
    //   Given a list and an editor that load asynchronously
    //   When each is loading
    //   Then its skeleton approximates the final layout (rows / editor blocks)
    //   And the transition to content is not jarring
    //
    // Manual checklist:
    //   [ ] Open a project whose Resources list is loading — skeleton shows row-shaped placeholders.
    //   [ ] Open an artifact editor while it loads — skeleton shows editor-block-shaped placeholders.
    //   [ ] When data arrives, the skeleton is replaced without a flash/jump (no jarring transition).
    [Fact(Skip = "@manual — visual skeleton-shape checklist, verified by a human during PR review.")]
    [Trait("Category", "manual")]
    public void Skeleton_loaders_match_the_shape_of_the_content_they_precede()
    {
    }

    // Scenario: Empty, loading, and error states are visually consistent across views
    //   Given the app across its primary views
    //   Then empty, loading, and error states use the same styled components and tone
    //   And none shows unstyled default WPF chrome
    //
    // Manual checklist:
    //   [ ] Projects home, Resources, Conversations, Artifacts each use the shared EmptyState control.
    //   [ ] Loading panes use the shared SkeletonLoader control (no blank pane anywhere).
    //   [ ] Error panes use the shared ErrorState control with a recovery button and a consistent tone.
    //   [ ] No view shows unstyled default WPF chrome (buttons/borders match the design system).
    [Fact(Skip = "@manual — cross-view visual-consistency checklist, verified by a human during PR review.")]
    [Trait("Category", "manual")]
    public void Empty_loading_and_error_states_are_visually_consistent_across_views()
    {
    }
}
