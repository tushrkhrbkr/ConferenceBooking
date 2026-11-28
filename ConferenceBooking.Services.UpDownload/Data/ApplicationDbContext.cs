
using Microsoft.EntityFrameworkCore;
using ConferenceBooking.Services.UpDownload.Models;

namespace ConferenceBooking.Services.UpDownload.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<FeedbackDocuments> Tbl_Feedback_DocumentRepository
            => Set<FeedbackDocuments>();

        public DbSet<ReportDocuments> Tbl_Report_DocumentRepository
            => Set<ReportDocuments>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<FeedbackDocuments>(entity =>
            {
                entity.ToTable("Tbl_Feedback_DocumentRepository");

                entity.HasKey(x => x.DocumentID);

                entity.Property(x => x.DocumentID)
                    .HasDefaultValueSql("NEWID()");

                entity.Property(x => x.DocumentAutoID)
                    .UseIdentityColumn(1000, 1);

                entity.Property(x => x.UploadedOn)
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.Property(x => x.DocumentData)
                    .HasColumnType("varbinary(max)");
            });

            modelBuilder.Entity<ReportDocuments>(entity =>
            {
                entity.ToTable("Tbl_Report_DocumentRepository");

                entity.HasKey(x => x.DocumentID);

                entity.Property(x => x.DocumentID)
                    .HasDefaultValueSql("NEWID()");

                entity.Property(x => x.DocumentAutoID)
                    .UseIdentityColumn(1000, 1);

                entity.Property(x => x.UploadedOn)
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.Property(x => x.DocumentData)
                    .HasColumnType("varbinary(max)");
            });
        }
    }
}
