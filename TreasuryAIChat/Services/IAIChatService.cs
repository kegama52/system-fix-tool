using System.Collections.Generic;

namespace TreasuryAIChat.Services;

/// <summary>Streaming AI chat-service contract.</summary>
/// <remarks>Each response is a sequence of text chunks; the last chunk carries <see cref="IsFinal"/> = true.</remarks>
public interface IAIChatService
{
    IAsyncEnumerable<string> GetResponseAsync(
        string message,
        string conversationId,
        System.Threading.CancellationToken ct = default);

    Task<string> GetEscalationSummaryAsync(
        string conversationId,
        System.Threading.CancellationToken ct = default);
}
