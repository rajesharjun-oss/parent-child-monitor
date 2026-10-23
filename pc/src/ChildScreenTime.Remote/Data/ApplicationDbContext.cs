using ChildScreenTime.Remote.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChildScreenTime.Remote.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ChildDevice> ChildDevices => Set<ChildDevice>();
    public DbSet<PairingCode> PairingCodes => Set<PairingCode>();
    public DbSet<UsageAggregate> UsageAggregates => Set<UsageAggregate>();
    public DbSet<BrowserActivityAggregate> BrowserActivityAggregates => Set<BrowserActivityAggregate>();
    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity => entity.Property(value => value.DisplayName).HasMaxLength(100));
        builder.Entity<ChildDevice>(entity =>
        {
            entity.Property(value => value.Name).HasMaxLength(100);
            entity.Property(value => value.TokenHash).HasMaxLength(64).IsFixedLength();
            entity.HasIndex(value => value.OwnerUserId);
            entity.HasOne(value => value.Owner).WithMany().HasForeignKey(value => value.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<PairingCode>(entity =>
        {
            entity.Property(value => value.CodeHash).HasMaxLength(64).IsFixedLength();
            entity.HasIndex(value => value.CodeHash).IsUnique();
            entity.HasIndex(value => new { value.OwnerUserId, value.ExpiresUtc });
            entity.HasOne(value => value.Owner).WithMany().HasForeignKey(value => value.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<UsageAggregate>(entity =>
        {
            entity.Property(value => value.ProcessName).HasMaxLength(128);
            entity.Property(value => value.DisplayName).HasMaxLength(200);
            entity.HasIndex(value => new { value.DeviceId, value.LocalDate, value.ProcessName }).IsUnique();
            entity.HasIndex(value => new { value.DeviceId, value.LocalDate });
            entity.HasOne(value => value.Device).WithMany(value => value.Usage).HasForeignKey(value => value.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<BrowserActivityAggregate>(entity =>
        {
            entity.Property(value => value.VideoId).HasMaxLength(11).IsFixedLength();
            entity.Property(value => value.Title).HasMaxLength(200);
            entity.Property(value => value.Channel).HasMaxLength(120);
            entity.HasIndex(value => new { value.DeviceId, value.LocalDate, value.VideoId }).IsUnique();
            entity.HasIndex(value => new { value.DeviceId, value.LocalDate });
            entity.HasOne(value => value.Device).WithMany(value => value.BrowserActivity).HasForeignKey(value => value.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<SecurityAuditEvent>(entity =>
        {
            entity.Property(value => value.EventType).HasMaxLength(80);
            entity.Property(value => value.Summary).HasMaxLength(500);
            entity.HasIndex(value => new { value.OwnerUserId, value.TimestampUtc });
            entity.HasOne(value => value.Owner).WithMany().HasForeignKey(value => value.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
