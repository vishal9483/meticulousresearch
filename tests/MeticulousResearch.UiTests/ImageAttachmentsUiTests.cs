using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/image-attachments/tests.md (SPEC §3.2.1, §4): attached images
/// render as inline thumbnails in the composer and in the sent user turn, and clicking a thumbnail
/// opens a larger preview. Driven through the real WPF window via FlaUI (UIA3), so these are tagged
/// <c>Category=ui</c> and excluded from the headless gate; they must compile and build. The composer
/// and thread surfaces are owned by the <c>conversations</c>/<c>streaming</c> M2 features, so the
/// helpers that open them fail loudly with a <see cref="NotSupportedException"/> naming that owner
/// until they are wired — these tests are never silently green.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class ImageAttachmentsUiTests
{
    private readonly ShellUiFixture _fixture;

    public ImageAttachmentsUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: Attached images render as inline thumbnails in the composer and the sent turn
    //   Given I attach an image in the composer
    //   Then a thumbnail is shown in the composer
    //   And after sending, the user turn shows the image as an inline thumbnail
    [Fact]
    public void Attached_images_render_as_inline_thumbnails_in_the_composer_and_the_sent_turn()
    {
        var composer = OpenComposer();

        // a thumbnail is shown in the composer
        var composerThumbnail = composer.FindFirstDescendant(cf => cf.ByAutomationId("ComposerAttachmentThumbnail"));
        Assert.NotNull(composerThumbnail);

        // after sending, the user turn shows the image as an inline thumbnail
        var thread = OpenThread();
        var turnThumbnail = thread.FindFirstDescendant(cf => cf.ByAutomationId("TurnAttachmentThumbnail"));
        Assert.NotNull(turnThumbnail);
    }

    // Scenario: Clicking a thumbnail opens a larger preview
    //   Given a sent turn with an image thumbnail
    //   When I click the thumbnail
    //   Then a larger preview of the image is shown
    [Fact]
    public void Clicking_a_thumbnail_opens_a_larger_preview()
    {
        var thread = OpenThread();

        var thumbnail = thread.FindFirstDescendant(cf => cf.ByAutomationId("TurnAttachmentThumbnail"));
        Assert.NotNull(thumbnail);

        // When I click the thumbnail
        thumbnail!.AsButton().Invoke();

        // Then a larger preview of the image is shown
        var preview = _fixture.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("ImageAttachmentPreview"));
        Assert.NotNull(preview);
    }

    /// <summary>
    /// Opens the composer that hosts image-attachment thumbnails. Owned by the M2
    /// conversations/streaming features; fails loudly until wired so this never passes silently.
    /// </summary>
    private AutomationElement OpenComposer()
    {
        var window = _fixture.MainWindow;
        return window.FindFirstDescendant(cf => cf.ByAutomationId("ConversationComposer"))
            ?? throw new NotSupportedException(
                "The composer that hosts image-attachment thumbnails is owned by the conversations/streaming " +
                "(M2) features; wire this helper to the composer surface when it lands.");
    }

    /// <summary>
    /// Opens the conversation thread that renders sent-turn thumbnails. Owned by the M2
    /// conversations/streaming features; fails loudly until wired so this never passes silently.
    /// </summary>
    private AutomationElement OpenThread()
    {
        var window = _fixture.MainWindow;
        return window.FindFirstDescendant(cf => cf.ByAutomationId("ConversationThread"))
            ?? throw new NotSupportedException(
                "The conversation thread that renders sent-turn image thumbnails is owned by the " +
                "conversations/streaming (M2) features; wire this helper to the thread surface when it lands.");
    }
}
