using System.Collections.Concurrent;
using TreasuryAIChat.Models;

namespace TreasuryAIChat.Services;

/// <summary>
/// Persists in-memory and durable chat transcripts.
/// Every live conversation is kept in memory for the active session;
/// completed ones can be flushed to SQLite (see ChatTranscriptStore).
/// </summary>
public class ConversationStore
{
    private readonly ConcurrentDictionary<string, List<ChatMessageDto>> _conversations = new();
    private readonly ConcurrentDictionary<string, AgentHandoffRequest>  _handoffs       = new();

    public IReadOnlyList<ChatMessageDto> GetOrCreate(string conversationId)
    {
        return _conversations.GetOrAdd(conversationId, _ => new());
    }

    public void Add(ChatMessageDto msg)
    {
        _conversations.AddOrUpdate(msg.ConversationId,
            _ => new List<ChatMessageDto> { msg },
            (_, list) => { list.Add(msg); return list; });
    }

    public void CompleteAiMessage(string conversationId, string content)
    {
        if (!_conversations.TryGetValue(conversationId, out var list)) return;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Sender == "ai" && !list[i].IsComplete)
            {
                list[i] = list[i] with { IsComplete = true, Content = content };
                return;
            }
        }
    }

    public bool TryRequestHandoff(string conversationId, AgentHandoffRequest request)
    {
        return _handoffs.TryAdd(conversationId, request);
    }

    public AgentHandoffRequest? GetHandoff(string conversationId)
    {
        _handoffs.TryGetValue(conversationId, out var req);
        return req;
    }

    public IReadOnlyDictionary<string, AgentHandoffRequest> GetAllHandoffs() => _handoffs;
}
