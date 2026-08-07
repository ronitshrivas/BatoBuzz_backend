using BatoBuzz.Awards.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BatoBuzz.Awards.Data;

public class AwardsDbContext : DbContext
{
    public AwardsDbContext(DbContextOptions<AwardsDbContext> options) : base(options) { }

    public DbSet<AwardConfig> Configs => Set<AwardConfig>();
    public DbSet<AwardParticipant> Participants => Set<AwardParticipant>();
    public DbSet<AwardVote> Votes => Set<AwardVote>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AwardConfig>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.IsActive);
            e.Property(x => x.Season).HasMaxLength(64);
            e.Property(x => x.Title).HasMaxLength(200);
        });

        b.Entity<AwardParticipant>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Season, x.MerchantId }).IsUnique();  // one entry per merchant per season
            e.HasIndex(x => new { x.Season, x.Status, x.VoteCount });    // leaderboard of approved
            e.Property(x => x.Season).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Pitch).HasMaxLength(1000);
        });

        b.Entity<AwardVote>(e =>
        {
            e.HasKey(x => new { x.Season, x.VoterId });   // one vote per user per season
            e.HasIndex(x => x.ParticipantId);
            e.Property(x => x.Season).HasMaxLength(64);
        });

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