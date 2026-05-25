namespace TreasuryAIChat.Models;

/// <summary>Single chat message across the SignalR pipeline.</summary>
public record ChatMessageDto(
    string Id,
    string ConversationId,
    string Sender,          // "user" | "ai" | "system" | "agent"
    string Content,
    DateTimeOffset Timestamp,
    bool IsComplete = true)
{
    /// <summary>Display name — mirrors <see cref="Sender"/> by default.</summary>
    public string? From { get; init; }

    /// <summary>HH:mm formatted time string for the UI.</summary>
    public string? Time { get; init; }

    /// <summary>CSS class used to colour the bubble on the client.</summary>
    public string? CssClass { get; init; }
}

/// <summary>Agent handoff request payload.</summary>
public record AgentHandoffRequest(
    string ConversationId,
    string? UserName,
    string? Department,
    string LastMessage)
{
    /// <summary>UTC timestamp formatted as "yyyy-MM-dd HH:mm".</summary>
    public string ActiveAt { get; init; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
}

/// <summary>Knowledge-base search result.</summary>
public record KBResult(
    string Title,
    string Content,
    float Score
);
