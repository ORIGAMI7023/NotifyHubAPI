using Microsoft.EntityFrameworkCore;
using NotifyHubAPI.Models;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace NotifyHubAPI.Data
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
            : base(options)
        {
        }

        public DbSet<EmailRecord> EmailRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // EmailRecord 配置
            modelBuilder.Entity<EmailRecord>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .ValueGeneratedOnAdd();

                entity.Property(e => e.ToAddresses)
                    .IsRequired()
                    .HasMaxLength(1000);

                entity.Property(e => e.CcAddresses)
                    .HasMaxLength(1000);

                entity.Property(e => e.BccAddresses)
                    .HasMaxLength(1000);

                entity.Property(e => e.Subject)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(e => e.Body)
                    .IsRequired();

                entity.Property(e => e.Category)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.ErrorMessage)
                    .HasMaxLength(2000);

                entity.Property(e => e.ApiKey)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.RequestId)
                    .HasMaxLength(50);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()");

                // 索引
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.ApiKey);
                entity.HasIndex(e => new { e.Status, e.RetryCount });
            });
        }
    }
}