using Microsoft.EntityFrameworkCore;
using Whitelabel.Infrastructure.Persistence.Entities;

namespace Whitelabel.Infrastructure.Persistence;

public sealed class WhitelabelDbContext(DbContextOptions<WhitelabelDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationAdminEntity> ApplicationAdmins => Set<ApplicationAdminEntity>();
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<TenantHostNameEntity> TenantHostNames => Set<TenantHostNameEntity>();
    public DbSet<TenantEmailDomainEntity> TenantEmailDomains => Set<TenantEmailDomainEntity>();
    public DbSet<TenantAdminEntity> TenantAdmins => Set<TenantAdminEntity>();
    public DbSet<TenantUserGrantEntity> TenantUserGrants => Set<TenantUserGrantEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationAdminEntity>(e =>
        {
            e.ToTable("ApplicationAdmins");
            e.HasKey(x => x.ObjectId);
            e.Property(x => x.ObjectId).HasMaxLength(128);
        });

        modelBuilder.Entity<TenantEntity>(e =>
        {
            e.ToTable("Tenants");
            e.HasKey(x => x.TenantId);
            e.Property(x => x.TenantId).HasMaxLength(128);
            e.Property(x => x.TenantName).HasMaxLength(512);
            e.Property(x => x.PrimaryColor).HasMaxLength(32);
            e.Property(x => x.SecondaryColor).HasMaxLength(32);
            e.Property(x => x.LogoUrl).HasMaxLength(2048);
            e.Property(x => x.Domain).HasMaxLength(256);
            e.Property(x => x.EntraTenantId).HasMaxLength(64);
        });

        modelBuilder.Entity<TenantHostNameEntity>(e =>
        {
            e.ToTable("TenantHostNames");
            e.HasKey(x => x.Id);
            e.Property(x => x.HostName).HasMaxLength(256);
            e.HasIndex(x => x.HostName).IsUnique();
            e.HasOne(x => x.Tenant)
                .WithMany(x => x.HostNames)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantEmailDomainEntity>(e =>
        {
            e.ToTable("TenantEmailDomains");
            e.HasKey(x => x.Id);
            e.Property(x => x.Domain).HasMaxLength(256);
            e.HasIndex(x => x.Domain).IsUnique();
            e.HasOne(x => x.Tenant)
                .WithMany(x => x.EmailDomains)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantAdminEntity>(e =>
        {
            e.ToTable("TenantAdmins");
            e.HasKey(x => new { x.TenantId, x.ObjectId });
            e.Property(x => x.ObjectId).HasMaxLength(128);
            e.HasOne(x => x.Tenant)
                .WithMany(x => x.TenantAdmins)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantUserGrantEntity>(e =>
        {
            e.ToTable("TenantUserGrants");
            e.HasKey(x => new { x.TenantId, x.ObjectId });
            e.Property(x => x.ObjectId).HasMaxLength(128);
            e.HasOne(x => x.Tenant)
                .WithMany(x => x.UserGrants)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
