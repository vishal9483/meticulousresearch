namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// A single rendered turn in the conversation thread (SPEC §3.3). Carries the role and text plus
/// simple role flags the view binds to for left/right alignment. Streaming, per-turn actions, and
/// the cost badge are layered on by <c>streaming</c> / <c>turn-metadata-actions</c>.
/// </summary>
public sealed class ConversationTurnViewModel
{
    /// <summary>Creates a turn view-model for the given role and content, optionally recording the model that produced it.</summary>
    /// <param name="role">The turn role (<c>user</c> or <c>assistant</c>).</param>
    /// <param name="content">The turn text.</param>
    /// <param name="model">The model id that produced an assistant turn (model-selector), or <c>null</c>.</param>
    public ConversationTurnViewModel(string role, string content, string? model = null)
    {
        Role = role ?? throw new ArgumentNullException(nameof(role));
        Content = content ?? "";
        Model = model;
    }

    /// <summary>The model id that produced this (assistant) turn, or <c>null</c> for user turns / unknown.</summary>
    public string? Model { get; }

    /// <summary>Whether a model label should be shown for this turn (assistant turns with a recorded model).</summary>
    public bool HasModel => IsAssistant && !string.IsNullOrWhiteSpace(Model);

    /// <summary>The model label shown on the assistant turn (the recorded model id, or empty).</summary>
    public string ModelLabel => Model ?? "";

    /// <summary>The turn role: <c>user</c> or <c>assistant</c>.</summary>
    public string Role { get; }

    /// <summary>The turn text.</summary>
    public string Content { get; }

    /// <summary>Whether this is a user turn.</summary>
    public bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether this is an assistant turn.</summary>
    public bool IsAssistant => !IsUser;

    /// <summary>A short label shown above the turn text.</summary>
    public string RoleLabel => IsUser ? "You" : "Assistant";
}
