namespace TreasuryAIChat.Models;

/// <summary>Single chat message across the SignalR pipeline.</summary>
public record ChatMessageDto(
    string Id,
    string ConversationId,
    string Sender,          // "user" | "ai" | "system" | "agent"
    string Content,
    DateTimeOffset Timestamp,
    bool IsComplete = true
);

/// <summary>Agent handoff request payload.</summary>
public record AgentHandoffRequest(
    string ConversationId,
    string? UserName,
    string? Department,
    string LastMessage
);

/// <summary>Knowledge-base search result.</summary>
public record KBResult(
    string Title,
    string Content,
    float Score
);
