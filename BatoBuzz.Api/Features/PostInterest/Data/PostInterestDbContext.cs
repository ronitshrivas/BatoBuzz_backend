using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PostInterestEntity = BatoBuzz.PostInterest.Entities.PostInterest;

namespace BatoBuzz.PostInterest.Data;

public class PostInterestDbContext : DbContext
{
    public PostInterestDbContext(DbContextOptions<PostInterestDbContext> options) : base(options) { }

    public DbSet<PostInterestEntity> Interests => Set<PostInterestEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<PostInterestEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PostType).HasMaxLength(20);
            e.Property(x => x.UserName).HasMaxLength(200);
            e.Property(x => x.UserPhone).HasMaxLength(40);
            e.Property(x => x.UserEmail).HasMaxLength(200);
            e.Property(x => x.UserPhoto).HasMaxLength(1000);
            e.Property(x => x.PostTitle).HasMaxLength(300);
            e.Property(x => x.PostLocation).HasMaxLength(300);
            e.Property(x => x.MerchantName).HasMaxLength(200);
            e.Property(x => x.MerchantPhoto).HasMaxLength(1000);

            e.HasIndex(x => new { x.PostId, x.UserId }).IsUnique();
            e.HasIndex(x => x.PostId);   // interested-users list + count
            e.HasIndex(x => x.UserId);   // a user's own interests
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
