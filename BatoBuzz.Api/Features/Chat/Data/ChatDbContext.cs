using BatoBuzz.Chat.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BatoBuzz.Chat.Data;

public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options) { }

    public DbSet<ChatThread> Threads => Set<ChatThread>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ChatThread>(e =>
        {
            e.HasKey(x => x.Id);
            // One conversation per (user, merchant) pair.
            e.HasIndex(x => new { x.UserId, x.MerchantId }).IsUnique();
            // Thread lists are "my threads, most recently active first".
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.MerchantId);
            e.HasIndex(x => x.UpdatedAt);
            e.Property(x => x.LastMessageText).HasMaxLength(500);
        });

        b.Entity<ChatMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Thread).WithMany(t => t.Messages)
             .HasForeignKey(x => x.ThreadId).OnDelete(DeleteBehavior.Cascade);
            // Message history is paged per thread, newest first.
            e.HasIndex(x => new { x.ThreadId, x.CreatedAt });
            e.Property(x => x.Text).HasMaxLength(4000);
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.MimeType).HasMaxLength(128);
        });

        ApplyUtcConverter(b);
    }

    private static void ApplyUtcConverter(ModelBuilder b)
    {
        var utc = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var utcN = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : v.Value.ToUniversalTime()) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
        foreach (var et in b.Model.GetEntityTypes())
            foreach (var p in et.GetProperties())
            {
                if (p.ClrType == typeof(DateTime)) p.SetValueConverter(utc);
                else if (p.ClrType == typeof(DateTime?)) p.SetValueConverter(utcN);
            }
    }
}