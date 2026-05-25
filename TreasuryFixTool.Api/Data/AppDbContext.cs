using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TreasuryFixTool.Api.Models;

namespace TreasuryFixTool.Api.Data;

/// <summary>
/// EF Core DbContext for the TreasuryFixTool authentication and user-management schema.
/// </summary>
public class AppDbContext : IdentityDbContext<User>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<AuditLog>   AuditLogs   => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── User customisations ─────────────────────────────────────────────────
        builder.Entity<User>(entity =>
        {
            entity.ToTable("users");                        // snake_case table
            entity.Property(u => u.FullName).IsRequired().HasMaxLength(200);
            entity.Property(u => u.Department).HasMaxLength(100);
            entity.Property(u => u.IsActive).HasDefaultValue(true);
            entity.Property(u => u.LockoutReason).HasMaxLength(500);
        });

        // ── AuditLog ───────────────────────────────────────────────────────────
        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Action).IsRequired().HasMaxLength(100);
            entity.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(a => a.IpAddress).HasMaxLength(45);
            entity.Property(a => a.UserAgent).HasMaxLength(512);
            entity.Property(a => a.ErrorMessage).HasMaxLength(1024);

            entity.HasOne<User>()
                  .WithMany(u => u.AuditLogs)
                  .HasForeignKey(a => a.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ── RefreshToken ───────────────────────────────────────────────────────
        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.Token).IsUnique();
            entity.HasOne(rt => rt.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(rt => rt.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
