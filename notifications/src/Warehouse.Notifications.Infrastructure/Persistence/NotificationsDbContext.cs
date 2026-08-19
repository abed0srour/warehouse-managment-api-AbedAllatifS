using Microsoft.EntityFrameworkCore;
using Warehouse.Notifications.Domain.Entities;

namespace Warehouse.Notifications.Infrastructure.Persistence
{
    public class NotificationsDbContext : DbContext
    {
        public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
            : base(options)
        {
        }

        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(n => n.Id);
                entity.Property(n => n.Type).IsRequired().HasMaxLength(100);
                entity.Property(n => n.Title).IsRequired().HasMaxLength(200);
                entity.Property(n => n.Message).IsRequired().HasMaxLength(1000);
                entity.Property(n => n.Severity).IsRequired().HasMaxLength(50);
                entity.Property(n => n.RelatedEntityId).IsRequired().HasMaxLength(100);
                entity.Property(n => n.RelatedEntityType).IsRequired().HasMaxLength(100);
                entity.Property(n => n.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
                entity.Property(n => n.EventId).HasMaxLength(100);
                entity.HasIndex(n => n.EventId).IsUnique();
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
