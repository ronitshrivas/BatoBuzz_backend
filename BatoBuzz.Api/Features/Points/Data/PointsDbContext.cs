using System.Text.Json;
using BatoBuzz.Points.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BatoBuzz.Points.Data;

public class PointsDbContext : DbContext
{
    public PointsDbContext(DbContextOptions<PointsDbContext> options) : base(options) { }

    public DbSet<UserPoints> UserPoints => Set<UserPoints>();
    public DbSet<PointHistoryEntry> History => Set<PointHistoryEntry>();
    public DbSet<PointLatch> Latches => Set<PointLatch>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<UserPoints>(e =>
        {
            e.HasKey(x => x.UserId);

            // Leaderboard reads order by points desc — index it.
            e.HasIndex(x => x.TotalPoints);

            // Achievements are a small inline list, stored as JSON to match the
            // app's array-on-the-document shape without a second table.
            var opts = new JsonSerializerOptions();
            e.Property(x => x.Achievements)
             .HasColumnType("jsonb")
             .HasConversion(
                 v => JsonSerializer.Serialize(v, opts),
                 v => JsonSerializer.Deserialize<List<Achievement>>(v, opts) ?? new List<Achievement>(),
                 new ValueComparer<List<Achievement>>(
                     (a, c) => JsonSerializer.Serialize(a, opts) == JsonSerializer.Serialize(c, opts),
                     v => JsonSerializer.Serialize(v, opts).GetHashCode(),
                     v => JsonSerializer.Deserialize<List<Achievement>>(JsonSerializer.Serialize(v, opts), opts)!));
        });

        b.Entity<PointHistoryEntry>(e =>
        {
            e.Property(x => x.TargetId).HasMaxLength(64);
            // History list is per user, newest first; "today" filters on date.
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
        });

        b.Entity<PointLatch>(e =>
        {
            e.Property(x => x.TargetId).HasMaxLength(64).IsRequired();
            // The anti-farming guarantee, enforced by the database.
            e.HasIndex(x => new { x.UserId, x.Action, x.TargetId }).IsUnique();
        });

        ApplyUtcDateTimeConverter(b);
    }

    private static void ApplyUtcDateTimeConverter(ModelBuilder b)
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