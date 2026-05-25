namespace TreasuryAIChat.Services;

/// <summary>Minimal stub — Ollama is not active in this build.</summary>
public class OllamaChatService : IAIChatService
{
    public async IAsyncEnumerable<string> GetResponseAsync(
        string message, string conversationId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken ct = default)
    {
        yield return "[Ollama is not configured in this build. Switch AI:Provider to mock in appsettings.json.]";
    }

    public Task<string> GetEscalationSummaryAsync(string conversationId, System.Threading.CancellationToken ct = default)
        => Task.FromResult("Ollama not configured.");
}
