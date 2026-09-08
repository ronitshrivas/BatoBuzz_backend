using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using FollowEntity = BatoBuzz.Follow.Entities.Follow;

namespace BatoBuzz.Follow.Data;

public class FollowDbContext : DbContext
{
    public FollowDbContext(DbContextOptions<FollowDbContext> options) : base(options) { }

    public DbSet<FollowEntity> Follows => Set<FollowEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<FollowEntity>(e =>
        {
            e.HasKey(x => x.Id);
            // One follow per (user, merchant); re-following is a no-op upsert.
            e.HasIndex(x => new { x.UserId, x.MerchantId }).IsUnique();
            e.HasIndex(x => x.MerchantId);   // follower count / follower list
            e.HasIndex(x => x.UserId);       // a user's following list
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
