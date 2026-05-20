using Microsoft.AspNetCore.SignalR;
using System.Text;
using TreasuryAIChat.Models;
using TreasuryAIChat.Services;

namespace TreasuryAIChat.Hubs;

/// <summary>
/// SignalR hub: real-time chat between a browser user, the AI assistant,
/// and optional human ICT support agents.
/// </summary>
public class ChatHub : Hub
{
    private readonly ILogger<ChatHub>       _log;
    private readonly IAIChatService         _ai;
    private readonly IKnowledgeBaseService  _kb;
    private readonly ConversationStore      _conv;
    private readonly IAuditLogger           _audit;

    // ConnectionId → ConversationId映射
    private static readonly Dictionary<string, string> ActiveConnections = new();

    public ChatHub(ILogger<ChatHub> log, IAIChatService ai, IKnowledgeBaseService kb,
                   ConversationStore conv, IAuditLogger audit)
    {
        _log       = log;
        _ai        = ai;
        _kb        = kb;
        _conv      = conv;
        _audit     = audit;
    }

    public override async Task OnConnectedAsync()
    {
        _log.LogInformation("SignalR connected: {ConnId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        _log.LogInformation("SignalR disconnected: {ConnId}", Context.ConnectionId);
        ActiveConnections.Remove(Context.ConnectionId);
        await base.OnDisconnectedAsync(ex);
    }

    // ── User → AI pipeline ────────────────────────────────────────────────────

    public async Task<string> SendUserMessage(string message, string conversationId)
    {
        if (string.IsNullOrWhiteSpace(message)) return string.Empty;

        // 1 — echo user message back
        ActiveConnections[Context.ConnectionId] = conversationId;
        await Clients.Caller.SendAsync("ReceiveUserMessage", new
        {
            Id        = Guid.NewGuid().ToString(),
            message,
            Time      = DateTime.Now.ToString("HH:mm"),
            From      = "You"
        });

        // 2 — knowledge-base suggestions (advisory only)
        var hints = await _kb.SearchAsync(message, 3);
        if (hints.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var h in hints)
            {
                sb.AppendLine($"• **{h.Title}**  (relevance {Math.Round(h.Score * 100)} %)");
                sb.AppendLine(Truncate(h.Content, 200) + "\n");
            }
            await Clients.Caller.SendAsync("ShowKBSuggestions", new { Suggestions = sb.ToString().TrimEnd() });
        }

        // 3 — stream AI response
        var fullReply = new StringBuilder();
        var aiMsgs = _ai.GetResponseAsync(message, conversationId, Context.ConnectionAborted);
        await foreach (var chunk in aiMsgs)
        {
            fullReply.Append(chunk);
            await Clients.Caller.SendAsync("ReceiveAIResponseChunk", new { Chunk = chunk, Final = false });
        }

        var aiMsg = new ChatMessageDto(Guid.NewGuid().ToString(), conversationId, "ai",
            fullReply.ToString(), DateTimeOffset.UtcNow, true)
        {
            From = "AI Support", Time = DateTime.Now.ToString("HH:mm"), CssClass = "ai"
        };
        _conv.Add(aiMsg);

        await Clients.Caller.SendAsync("ReceiveAIResponseChunk", new
        {
            Chunk    = aiMsg.Content,
            Final    = true,
            Time     = aiMsg.Time,
            From     = aiMsg.From
        });

        _ = _audit.LogAsync(new AuditEntry("user_message", conversationId, message[..Math.Min(300, message.Length)]));
        return aiMsg.Id;
    }

    // ── Agent handoff ─────────────────────────────────────────────────────────

    public async Task RequestAgentHandoff(string conversationId)
    {
        if (!ActiveConnections.TryGetValue(Context.ConnectionId, out var cid) || cid != conversationId)
            return;

        var lastMsg = _conv.GetOrCreate(conversationId).LastOrDefault()?.Content ?? "—";
        var request = new AgentHandoffRequest(conversationId, "User", null,
                                              lastMsg[..Math.Min(200, lastMsg.Length)]);
        _conv.TryRequestHandoff(conversationId, request);

        await Clients.All.SendAsync("AgentHandoffRequested", new { request.ConversationId, request.LastMessage, ActiveAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") });
        await Clients.Caller.SendAsync("Reply", new { Content = "An ICT support agent is being notified. Please hold.", Time = DateTime.UtcNow.ToString("HH:mm"), From = "System" });

        _ = _audit.LogAsync(new AuditEntry("agent_handoff", conversationId, request.LastMessage));
    }

    public async Task SendAgentMessage(string conversationId, string message)
    {
        var agentMsg = new ChatMessageDto(Guid.NewGuid().ToString(), conversationId, "agent",
            message, DateTimeOffset.UtcNow, true)
        {
            From = "ICT Support", Time = DateTime.Now.ToString("HH:mm"), CssClass = "agent"
        };
        _conv.Add(agentMsg);
        await Clients.Group(conversationId).SendAsync("ReceiveAgentMessage", new { agentMsg.Id, message, agentMsg.Time, From = agentMsg.From });
        _ = _audit.LogAsync(new AuditEntry("agent_message", conversationId, message[..Math.Min(300, message.Length)]));
    }

    public async Task JoinConversation(string conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId,
            Context.ConnectionAborted);
        await Clients.Caller.SendAsync("AgentJoined", new { ConversationId = conversationId, Message = $"Joined {conversationId[..8]}…" });
    }

    public async Task LeaveConversation(string conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId,
            Context.ConnectionAborted);
    }

    public async Task<IReadOnlyList<AgentHandoffRequest>> GetActiveHandoffs()
        => _conv.GetAllHandoffs().Values.ToList();

    public IReadOnlyList<ChatMessageDto> GetTranscript(string conversationId)
        => _conv.GetOrCreate(conversationId);

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
