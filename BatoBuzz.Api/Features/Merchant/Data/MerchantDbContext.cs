using BatoBuzz.Merchant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BatoBuzz.Merchant.Data;

public class MerchantDbContext : DbContext
{
    public MerchantDbContext(DbContextOptions<MerchantDbContext> options) : base(options) { }

    public DbSet<MerchantProfile> Merchants => Set<MerchantProfile>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<MerchantProfile>(e =>
        {
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.BusinessName).HasMaxLength(200).IsRequired();
            e.Property(x => x.BusinessEmail).HasMaxLength(200);
            e.Property(x => x.BusinessCategory).HasMaxLength(500);
            e.Property(x => x.BusinessPanNumber).HasMaxLength(50);
            e.Property(x => x.CityId).HasMaxLength(64);
            e.Property(x => x.CityName).HasMaxLength(120);
            e.Property(x => x.Ward).HasMaxLength(20);
            e.Property(x => x.RejectionReason).HasMaxLength(500);

            // One profile per Identity account, and phone is unique — the same
            // two guarantees the Firestore doc had (doc id = uid, phone unique).
            e.HasIndex(x => x.MerchantId).IsUnique();
            e.HasIndex(x => x.Phone).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => new { x.CityId, x.Status });
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
