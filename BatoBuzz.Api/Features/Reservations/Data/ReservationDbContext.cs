using BatoBuzz.Reservations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BatoBuzz.Reservations.Data;

public class ReservationDbContext : DbContext
{
    public ReservationDbContext(DbContextOptions<ReservationDbContext> options) : base(options) { }

    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<MerchantBlacklistEntry> Blacklist => Set<MerchantBlacklistEntry>();
    public DbSet<PostStock> PostStocks => Set<PostStock>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Reservation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ProductTitle).HasMaxLength(300);
            e.Property(x => x.ProductImage).HasMaxLength(1000);
            e.Property(x => x.ReservedPrice).HasColumnType("numeric(12,2)");
            e.Property(x => x.UserName).HasMaxLength(200);
            e.Property(x => x.UserPhoto).HasMaxLength(1000);
            e.Property(x => x.MerchantName).HasMaxLength(200);
            e.Property(x => x.MerchantPhoto).HasMaxLength(1000);

            // The three hottest access paths: a user's own list, a merchant's
            // list, and the "active hold for this (user, post)" lookup.
            e.HasIndex(x => new { x.UserId, x.Status });
            e.HasIndex(x => new { x.MerchantId, x.Status });
            e.HasIndex(x => new { x.UserId, x.PostId, x.Status });
            e.HasIndex(x => x.ExpiresAt);   // for the expiry sweep
        });

        b.Entity<MerchantBlacklistEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Reason).HasMaxLength(500);
            // One blacklist row per (merchant, user); re-blocking updates it.
            e.HasIndex(x => new { x.MerchantId, x.UserId }).IsUnique();
        });

        b.Entity<PostStock>(e =>
        {
            e.HasKey(x => x.PostId);
        });

        ApplyUtcDateTimeConverter(b);
    }

    /// Npgsql maps DateTime to `timestamp with time zone`, which throws unless
    /// every value is Kind=Utc. Values built in code or from JSON arrive as
    /// Unspecified — convert on the way in/out to avoid scattered 500s.
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
