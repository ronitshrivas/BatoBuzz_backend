using BatoBuzz.Feed.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BatoBuzz.Feed.Data;

public class FeedDbContext : DbContext
{
    public FeedDbContext(DbContextOptions<FeedDbContext> options)
        : base(options) { }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();
    public DbSet<PostComment> PostComments => Set<PostComment>();
    public DbSet<PostView> PostViews => Set<PostView>();
    public DbSet<PostReport> PostReports => Set<PostReport>();
    public DbSet<City> Cities => Set<City>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Post>(e =>
        {
            e.Property(x => x.MerchantName).HasMaxLength(160);
            e.Property(x => x.Category).HasMaxLength(80);
            e.Property(x => x.BusinessCategory).HasMaxLength(80);
            e.Property(x => x.AdsCategory).HasMaxLength(80);
            e.Property(x => x.CityId).HasMaxLength(64);
            e.Property(x => x.CityName).HasMaxLength(120);
            e.Property(x => x.Body).HasMaxLength(5000);

            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.PreviousPrice).HasPrecision(18, 2);
            e.Property(x => x.DiscountedPrice).HasPrecision(18, 2);
            e.Property(x => x.SalaryFrom).HasPrecision(18, 2);
            e.Property(x => x.SalaryTo).HasPrecision(18, 2);
            e.Property(x => x.EventPrice).HasPrecision(18, 2);

            // Feed queries always filter on these; composite indexes match the
            // (filter + sort) shapes the apps issue.
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.MerchantId);
            e.HasIndex(x => new { x.CityId, x.CreatedAt });
            e.HasIndex(x => new { x.PostType, x.CreatedAt });
            e.HasIndex(x => new { x.PostType, x.AdsCategory, x.CreatedAt });
            e.HasIndex(x => x.IsDeleted);
        });

        b.Entity<PostLike>(e =>
        {
            // Idempotent likes: a given actor can like a post at most once.
            e.HasIndex(x => new { x.PostId, x.ActorId }).IsUnique();
            e.HasOne(x => x.Post)
             .WithMany(p => p.Likes)
             .HasForeignKey(x => x.PostId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PostComment>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(2000).IsRequired();
            e.Property(x => x.AuthorName).HasMaxLength(160);

            e.HasIndex(x => new { x.PostId, x.ParentId, x.CreatedAt });

            e.HasOne(x => x.Post)
             .WithMany(p => p.Comments)
             .HasForeignKey(x => x.PostId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Parent)
             .WithMany(c => c.Replies)
             .HasForeignKey(x => x.ParentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PostView>(e =>
        {
            e.HasIndex(x => new { x.PostId, x.ViewerId }).IsUnique();
            e.HasOne(x => x.Post)
             .WithMany(p => p.Views)
             .HasForeignKey(x => x.PostId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PostReport>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasIndex(x => new { x.PostId, x.ReporterId }).IsUnique();
            e.HasOne(x => x.Post)
             .WithMany(p => p.Reports)
             .HasForeignKey(x => x.PostId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<City>(e =>
        {
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.Name);
        });

        ApplyUtcDateTimeConverter(b);
    }

    /// Npgsql maps DateTime to `timestamp with time zone`, which throws unless
    /// every value has Kind=Utc. Values read back from Postgres arrive as Utc,
    /// but values built in code (or deserialized from JSON) often come back as
    /// Unspecified/Local. Converting on the way in and out keeps this from
    /// surfacing as scattered 500s.
    private static void ApplyUtcDateTimeConverter(ModelBuilder b)
    {
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableUtcConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : v.Value.ToUniversalTime()) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(utcConverter);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(nullableUtcConverter);
            }
        }
    }
}
