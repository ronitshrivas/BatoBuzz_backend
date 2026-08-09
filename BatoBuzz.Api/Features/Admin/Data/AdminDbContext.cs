using BatoBuzz.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BatoBuzz.Admin.Data;

public class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

    public DbSet<AdminAuditEntry> AuditEntries => Set<AdminAuditEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AdminAuditEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => new { x.TargetType, x.TargetId });
            e.Property(x => x.Action).HasMaxLength(80);
            e.Property(x => x.TargetType).HasMaxLength(40);
            e.Property(x => x.ActorName).HasMaxLength(200);
            e.Property(x => x.Detail).HasMaxLength(2000);
        });

        var utc = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        foreach (var et in b.Model.GetEntityTypes())
            foreach (var p in et.GetProperties())
                if (p.ClrType == typeof(DateTime)) p.SetValueConverter(utc);
    }
}