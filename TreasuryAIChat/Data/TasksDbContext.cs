using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace TreasuryAIChat.Data;

/// <summary>
/// EF Core DbContext targeting tiisgs_db (PostgreSQL, port 5324).
/// Used for chat transcripts, audit logs, and the knowledge-base cache.
/// </summary>
public class TasksDbContext : DbContext
{
    public TasksDbContext(DbContextOptions<TasksDbContext> options) : base(options) { }

    public DbSet<ChatMessageEntity>  Messages       => Set<ChatMessageEntity>();
    public DbSet<AuditLogEntity>      AuditLogs      => Set<AuditLogEntity>();
    public DbSet<KnowledgeBaseEntity> KnowledgeBases => Set<KnowledgeBaseEntity>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<ChatMessageEntity>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => m.ConversationId);
            e.HasIndex(m => m.Timestamp);
            e.ToTable("chat_transcripts");
        });

        model.Entity<AuditLogEntity>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.ConversationId);
            e.HasIndex(a => a.EventType);
            e.ToTable("chat_audit_log");
        });

        model.Entity<KnowledgeBaseEntity>(e =>
        {
            e.HasKey(k => k.Id);
            e.ToTable("knowledge_base");
            e.Property(k => k.Tags).HasColumnType("text");
        });
    }
}

public record ChatMessageEntity
{
    public Guid   Id           { get; set; } = Guid.NewGuid();
    public string ConversationId { get; set; } = string.Empty;
    public string Sender       { get; set; } = string.Empty;
    public string Content      { get; set; } = string.Empty;
    public DateTime Timestamp  { get; set; } = DateTime.UtcNow;
    public bool   IsComplete   { get; set; } = true;
}

public record AuditLogEntity
{
    public Guid   Id            { get; set; } = Guid.NewGuid();
    public DateTime At          { get; set; } = DateTime.UtcNow;
    public string EventType     { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string Summary       { get; set; } = string.Empty;
}

public record KnowledgeBaseEntity
{
    public Guid    Id          { get; set; }
    public string  Title       { get; set; } = string.Empty;
    public string  Content     { get; set; } = string.Empty;
    public string  CategoryId  { get; set; } = string.Empty;
    public string? Tags        { get; set; }
    public string  IssueType   { get; set; } = string.Empty;
    public int     PriorityLevel { get; set; }
    public bool    IsActive    { get; set; } = true;
}
