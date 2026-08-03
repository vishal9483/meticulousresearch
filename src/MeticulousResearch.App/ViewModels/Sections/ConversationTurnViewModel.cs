namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// A single rendered turn in the conversation thread (SPEC §3.3). Carries the role and text plus
/// simple role flags the view binds to for left/right alignment. Streaming, per-turn actions, and
/// the cost badge are layered on by <c>streaming</c> / <c>turn-metadata-actions</c>.
/// </summary>
public sealed class ConversationTurnViewModel
{
    /// <summary>Creates a turn view-model for the given role and content.</summary>
    public ConversationTurnViewModel(string role, string content)
    {
        Role = role ?? throw new ArgumentNullException(nameof(role));
        Content = content ?? "";
    }

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
