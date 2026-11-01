using Microsoft.EntityFrameworkCore;
using ConferenceBooking.Services.EmailAPI.Models;
using ConferenceBooking.Services.EmailAPI.Models.Dto;

namespace ConferenceBooking.Services.EmailAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> Options) : base(Options)
        {

        }
        public DbSet<EmailTemplate> EmailTemplates { get; set; }

        public DbSet<EmailTemplateAudit> EmailTemplateAudits { get; set; }

        public DbSet<EmailNotificationOutbox> EmailNotificationOutbox { get; set; }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<EmailTemplate>()
                .ToTable("Tbl_EmailTemplate");

            modelBuilder.Entity<EmailTemplateAudit>()
                .ToTable("Tbl_EmailTemplateAudit");

            modelBuilder.Entity<EmailNotificationOutbox>(entity =>
            {
                entity.ToTable("Tbl_EmailNotificationOutbox");

                entity.HasKey(x => x.EmailNotificationOutboxId);

                entity.Property(x => x.Status)
                    .HasConversion<byte>();
            });

        }

    }
}
