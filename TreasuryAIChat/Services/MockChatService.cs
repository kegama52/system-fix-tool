using System.Runtime.CompilerServices;
using TreasuryAIChat.Models;

namespace TreasuryAIChat.Services;

/// <summary>Offline mock AI — works without any network dependency.</summary>
public class MockChatService : IAIChatService
{
    private static readonly Dictionary<string, string> _responses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["password"]  = "If you've forgotten your password, please visit the self-service password reset portal or contact the ICT helpdesk at extension 5500 for a manual reset. You will need to provide your employee ID and a copy of your ID document.",
        ["login"]     = "Try the following steps to resolve a login issue:\n\n1. Verify your caps-lock key is off.\n2. Make sure you are on the correct domain (TIISGS).\n3. Wait 5 minutes after 3 failed attempts before retrying.\n4. If the problem persists, open a support ticket via the system tray icon and select 'Account / Login Issues' as the category.",
        ["printer"]   = "Please check that the printer is powered on, the network cable is connected, and the correct printer driver is installed. If the status light is amber or red, try restarting the printer. If the issue continues, use the 'Print Issues' report in the main window and send it to support.",
        ["email"]     = "For email issues, first verify that Outlook is in 'Cached Exchange Mode'. Check your connection by opening a browser and loading any external site. If the problem is only in Outlook, clear the Offline Address Book (OAB) and restart Outlook.",
        ["vpn"]       = "When the VPN disconnects, reconfigure it using the FortiClient application found on your desktop. Ensure you are using your domain credentials. If connection still fails, confirm you are working from a TRN IP range or connected via the internal WI-FI.",
        ["ticket"]    = "To raise a support ticket, open the TreasuryFixTool, navigate to the relevant diagnostic tab, then follow the on-screen prompts to generate and email the ticket. The ticket will include your system diagnostics, logs, and all enabled monitoring modules.",
        ["escalate"]  = "To escalate this issue, click the 'Escalate' button in the chat panel. A system ticket will be created and all relevant diagnostic data will be attached automatically. An ICT agent will be assigned and you will receive a confirmation email.",
    };

    public async IAsyncEnumerable<string> GetResponseAsync(
        string message,
        string conversationId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken ct = default)
    {
        var lower = message.ToLowerInvariant();
        string reply;

        if (lower.Contains("hello") || lower.Contains("hi") || lower.Contains("good"))
        {
            reply = "Hello! I am the TreasuryFixTool AI assistant. I can help you with login issues, printer problems, email configuration, VPN connectivity, ticket generation, and many other system fixes. What seems to be the problem?";
        }
        else
        {
            var matched = _responses
                .Where(kv => lower.Contains(kv.Key))
                .OrderBy(kv => kv.Key.Length)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(matched.Value))
            {
                reply = matched.Value;
            }
            else
            {
                reply = "I'm not entirely sure about that specific issue, but I can help you create a support ticket so our ICT team can investigate. Would you like me to do that now? I'll run a full system diagnostic and include it with the ticket. Alternatively, you can press 'Escalate' at the bottom of this chat and I'll create the ticket automatically.";
            }
        }

        // Stream the reply in small chunks to mimic typing / token-stream behaviour.
        foreach (var chunk in ChunkText(reply))
        {
            ct.ThrowIfCancellationRequested();
            yield return chunk;
            await Task.Delay(30, ct);          // ~33 tokens/sec pacing
        }

        // Final marker chunk
        yield return "||FINAL||";
    }

    public Task<string> GetEscalationSummaryAsync(string conversationId, System.Threading.CancellationToken ct = default)
    {
        return Task.FromResult(
            $"Escalation requested for conversation {conversationId[..8]}… — " +
            "user authenticated via Windows domain, local diagnostics collected, " +
            "knowledge-base check attempted. Human agent review required.");
    }

    private static IEnumerable<string> ChunkText(string text)
    {
        var words  = text.Split(' ');
        var chunk  = new System.Text.StringBuilder();
        var count  = 0;

        foreach (var w in words)
        {
            if (count > 0 && count % 6 == 0)
            {
                yield return chunk.ToString();
                chunk.Clear();
            }
            if (count > 0) chunk.Append(' ');
            chunk.Append(w);
            count++;
        }

        if (chunk.Length > 0) yield return chunk.ToString();
    }
}
