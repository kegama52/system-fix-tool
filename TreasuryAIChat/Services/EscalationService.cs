using TreasuryAIChat.Models;

namespace TreasuryAIChat.Services;

/// <summary>Escalation decision record.</summary>
public record EscalationDecision(
    string ConversationId,
    bool ShouldEscalate,
    string Reason,
    string? AssignedAgentId = null);

/// <summary>Handles the decision whether a conversation needs to be escalated to a human agent.</summary>
public interface IAIEscalationService
{
    Task<EscalationDecision> EvaluateAsync(string conversationId, IReadOnlyList<ChatMessageDto> transcript,
                                           System.Threading.CancellationToken ct = default);
}

/// <summary>Keyword / length heuristic fallback when no LLM is configured.</summary>
public class MockEscalationService : IAIEscalationService
{
    private static readonly HashSet<string> _triggerWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "escalate", "speak to a human", "speak to someone", "manager",
        "supervisor", "complaint", "urgent", "legal", "fraud",
    };

    public Task<EscalationDecision> EvaluateAsync(
        string conversationId, IReadOnlyList<ChatMessageDto> transcript,
        System.Threading.CancellationToken ct = default)
    {
        var lastUserMsg = transcript.LastOrDefault(m => m.Sender == "user")?.Content ?? string.Empty;
        var shouldEscalate = _triggerWords.Any(w => lastUserMsg.Contains(w, StringComparison.OrdinalIgnoreCase))
                         || transcript.Count > 20;

        return Task.FromResult(new EscalationDecision(
            conversationId,
            shouldEscalate,
            shouldEscalate
                ? "User explicitly requested or conversation exceeded 20 turns."
                : "No escalation trigger detected by heuristic rules.",
            shouldEscalate ? "ict-support-01" : null));
    }
}
