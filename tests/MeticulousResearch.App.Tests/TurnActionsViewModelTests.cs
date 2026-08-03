using MeticulousResearch.App.Services;
using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Models;
using MeticulousResearch.Core.Turns;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit test for the turn "Copy" action (docs/features/turn-metadata-actions/tests.md, SPEC §3.3).
/// Window-free: it drives <see cref="TurnActionsViewModel"/> over a fake clipboard so the copy
/// behaviour is proven without a WPF/STA clipboard. (Retry/edit/promote/delete are proven against the
/// Core turn-action service; the cost badge is proven against the Core cost calculator.)
/// </summary>
public sealed class TurnActionsViewModelTests
{
    private sealed class FakeClipboardService : IClipboardService
    {
        public string? LastText { get; private set; }

        public void SetText(string text) => LastText = text;
    }

    private static TurnActionsViewModel NewActions(string text, IClipboardService clipboard) =>
        new(
            text,
            new TurnMetadata { Model = "claude-sonnet-5" },
            new CatalogTurnCostCalculator(ModelCatalogLoader.Default),
            clipboard);

    // Scenario: Copy places the assistant turn's text on the clipboard
    [Fact]
    public void Copy_places_the_assistant_turns_text_on_the_clipboard()
    {
        var clipboard = new FakeClipboardService();

        // Given a completed assistant turn with text "The TAM is $12B"
        var actions = NewActions("The TAM is $12B", clipboard);

        // When I copy the turn
        actions.CopyCommand.Execute(null);

        // Then the clipboard contains "The TAM is $12B"
        Assert.Equal("The TAM is $12B", clipboard.LastText);
    }
}
