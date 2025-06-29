using Auth.Service.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Auth.Service.Data;

public class AuthDbContext : IdentityDbContext<ApplicationUser>
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure RefreshToken entity
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(rt => rt.Id);
            entity.Property(rt => rt.Token).IsRequired().HasMaxLength(500);
            entity.Property(rt => rt.UserId).IsRequired();
            entity.HasIndex(rt => rt.Token).IsUnique();

            // Configure relationship
            entity.HasOne(rt => rt.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(rt => rt.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure ApplicationUser entity
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FirstName).IsRequired().HasMaxLength(50);
            entity.Property(u => u.LastName).IsRequired().HasMaxLength(50);
            entity.Property(u => u.CreatedAt).IsRequired();
            entity.Property(u => u.UpdatedAt).IsRequired();
            entity.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);
        });

        // Configure table names (optional - remove 'AspNet' prefix)
        modelBuilder.Entity<ApplicationUser>().ToTable("Users");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>().ToTable("Roles");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().ToTable("UserRoles");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("UserClaims");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().ToTable("UserLogins");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>().ToTable("RoleClaims");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().ToTable("UserTokens");
    }
}
