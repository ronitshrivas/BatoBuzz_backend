using BatoBuzz.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Identity.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<MerchantAccount> Merchants => Set<MerchantAccount>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.GoogleSubjectId);
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(120);
        });

        b.Entity<MerchantAccount>(e =>
        {
            e.HasIndex(x => x.Phone).IsUnique();
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.BusinessName).HasMaxLength(160);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.Token).IsUnique();
            e.HasOne(x => x.AppUser)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(x => x.AppUserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.MerchantAccount)
             .WithMany(m => m.RefreshTokens)
             .HasForeignKey(x => x.MerchantAccountId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}