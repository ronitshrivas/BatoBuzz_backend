using BatoBuzz.Notifications.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BatoBuzz.Notifications.Data;

public class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options) { }

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Notification>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RecipientId, x.CreatedAt });   // my notifications, newest first
            e.HasIndex(x => new { x.RecipientId, x.IsRead });      // unread count
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Body).HasMaxLength(1000);
            e.Property(x => x.ActorName).HasMaxLength(200);
        });

        b.Entity<DeviceToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Token).IsUnique();                   // one row per device token
            e.HasIndex(x => x.OwnerId);                            // all of an account's devices
            e.Property(x => x.Token).HasMaxLength(500);
        });

        var utc = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        foreach (var et in b.Model.GetEntityTypes())
            foreach (var p in et.GetProperties())
                if (p.ClrType == typeof(DateTime)) p.SetValueConverter(utc);
    }
}