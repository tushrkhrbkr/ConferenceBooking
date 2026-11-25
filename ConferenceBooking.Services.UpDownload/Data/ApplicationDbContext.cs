using Microsoft.EntityFrameworkCore;
using ConferenceBooking.Services.UpDownload.Models;

namespace ConferenceBooking.Services.UpDownload.Data
{
    public class ApplicationDbContext:DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> Options) : base(Options)
        {

        }

        public DbSet<FeedbackDocuments> Tbl_Feedback_DocumentRepository { get; set; }

        public DbSet<ReportDocuments> Tbl_Report_DocumentRepository { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            modelBuilder.Entity<FeedbackDocuments>()
              .Property(b => b.DocumentID)
              .HasDefaultValueSql("NEWID()");

            modelBuilder.Entity<FeedbackDocuments>()
                .Property(b => b.UploadedOn)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<ReportDocuments>()
                 .Property(b => b.DocumentID)
                 .HasDefaultValueSql("NEWID()");

            modelBuilder.Entity<ReportDocuments>()
                .Property(b => b.UploadedOn)
                .HasDefaultValueSql("GETDATE()");
        }
    }
}
