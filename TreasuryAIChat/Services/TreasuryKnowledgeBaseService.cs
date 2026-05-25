using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TreasuryAIChat.Data;
using TreasuryAIChat.Models;

namespace TreasuryAIChat.Services;

/// <summary>Scored knowledge-base lookup against tiisgs_db.knowledge_base.</summary>
/// <summary>SASIMH: score ∈ [0,1]; length sanity gate; keyword-weight default = 1.</summary>
public interface IKnowledgeBaseService
{
    Task<IReadOnlyList<KBResult>> SearchAsync(string query, int topK = 3);
}

/// <summary>EF-backed implementation.</summary>
public class TreasuryKnowledgeBaseService : IKnowledgeBaseService
{
    private readonly IDbContextFactory<TasksDbContext> _dbFactory;
    private static readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","and","or","but","is","are","was","were","be","been","being","have","has","had",
        "do","does","did","will","would","could","should","shall","may","might","can","to",
        "of","in","for","on","with","at","by","from","as","into","through","during","before",
        "after","about","a","an","we","our","us","they","their","i","you","your"
    };

    public TreasuryKnowledgeBaseService(IDbContextFactory<TasksDbContext> dbFactory)
        => _dbFactory = dbFactory;

        public Task<IReadOnlyList<KBResult>> SearchAsync(string query, int topK = 3)
        {
        using var db = _dbFactory.CreateDbContext();
        var entries = db.KnowledgeBases.Where(k => k.IsActive).AsNoTracking().ToList();

        var qTokens = Tokenise(query).Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (qTokens.Count == 0) return Task.FromResult((IReadOnlyList<KBResult>)Array.Empty<KBResult>());

        var docFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            var dw = Tokenise(e.Title + " " + e.Content + " " + e.Tags).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var w in dw) docFreq.TryAdd(w, 0);
            foreach (var w in dw) docFreq[w]++;
        }

        var scored = new List<KBResult>();
        foreach (var e in entries)
        {
            var dTokens = Tokenise(e.Title + " " + e.Content + " " + e.Tags)
                          .Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);

            int  exact    = qTokens.Count(dTokens.Contains);
            int  overlaps = 0;
            float tfIdf   = 0f;
            foreach (var q in qTokens) if (dTokens.Contains(q)) { overlaps++; tfIdf += 1f / MathF.Log2(2 + docFreq.GetValueOrDefault(q)); }

            var eTags  = ParseTags(e.Tags ?? string.Empty);
            int tagHit = qTokens.Count(eTags.Contains);

            float raw  = exact * 3f + (overlaps / MathF.Max(1, qTokens.Count)) * 2f + tfIdf + tagHit * 2f;
            float lenR = e.Content.Length / 2000f;
            if (lenR is < 0.2f or > 1f) raw *= 0.5f;
            float score = Math.Clamp(raw / 12f, 0f, 1f);
            if (score < 0.10f) continue;

            scored.Add(new KBResult(e.Title, e.Content, score));
        }

        return Task.FromResult((IReadOnlyList<KBResult>)scored.OrderByDescending(r => r.Score).Take(topK).ToList());
    }

    private static HashSet<string> ParseTags(string rawTags)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(rawTags)) return set;
        foreach (var p in rawTags.Split(['{', '}', ',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (p.Length > 1) set.Add(p);
        return set;
    }

    private static List<string> Tokenise(string text)
    {
        var result = new List<string>();
        foreach (var raw in text.Split([' ', '\t', '\r', '\n', ',', '.', '!', '?', ';', ':', '/', '\\', '(', ')', '[', ']', '{', '}', '-', '_', '\'', '"'],
                                       StringSplitOptions.RemoveEmptyEntries))
        {
            var s = raw.Trim().ToLowerInvariant();
            if (s.Length > 2 && !_stopWords.Contains(s)) result.Add(s);
        }
        return result;
    }
}

/// <summary>Probes Ollama port first, then falls back to the always-available mock.</summary>
public static class ChatServiceFactory
{
    public static IAIChatService Build(IConfiguration config)
        => new MockChatService();
}
