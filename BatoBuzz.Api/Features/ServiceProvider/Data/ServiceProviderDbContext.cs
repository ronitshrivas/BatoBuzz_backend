using BatoBuzz.ServiceProvider.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BatoBuzz.ServiceProvider.Data;

public class ServiceProviderDbContext : DbContext
{
    public ServiceProviderDbContext(DbContextOptions<ServiceProviderDbContext> options) : base(options) { }

    public DbSet<ServiceProviderEntity> Providers => Set<ServiceProviderEntity>();
    public DbSet<ProviderReviewEntity> Reviews => Set<ProviderReviewEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ServiceProviderEntity>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(160).IsRequired();
            e.Property(x => x.Profession).HasMaxLength(120);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.WhatsApp).HasMaxLength(20);
            e.Property(x => x.ServiceArea).HasMaxLength(160);
            e.Property(x => x.About).HasMaxLength(2000);
            e.Property(x => x.ReviewNote).HasMaxLength(500);

            // One application per user; the list filters on status + recency.
            e.HasIndex(x => x.SubmittedById).IsUnique();
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        b.Entity<ProviderReviewEntity>(e =>
        {
            e.Property(x => x.Author).HasMaxLength(160);
            e.Property(x => x.Comment).HasMaxLength(2000);

            // One review per (provider, user).
            e.HasIndex(x => new { x.ProviderId, x.UserId }).IsUnique();

            e.HasOne(x => x.Provider)
             .WithMany(p => p.Reviews)
             .HasForeignKey(x => x.ProviderId)
             .OnDelete(DeleteBehavior.Cascade);
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