using ConferenceBooking.Services.BookingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ConferenceBooking.Services.BookingAPI.Data
{
    /// <summary>
    /// BookingAPI data context.
    ///
    /// This context maps BookingAPI-owned tables and maps the existing shared
    /// AuthAPI tables without including them in BookingAPI migrations.
    /// It deliberately does not inherit from IdentityDbContext and does not
    /// create or manage ASP.NET Core Identity tables.
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Shared tables (read/write as required by application services).
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<Department> Departments => Set<Department>();
        public DbSet<EmailNotificationOutbox> EmailNotificationOutbox => Set<EmailNotificationOutbox>();

        // Halls and configuration.
        public DbSet<ConferenceHall> ConferenceHalls => Set<ConferenceHall>();
        public DbSet<HallManagerMap> HallManagerMaps => Set<HallManagerMap>();
        public DbSet<HallSubAdminMap> HallSubAdminMaps => Set<HallSubAdminMap>();
        public DbSet<HallDepartmentMap> HallDepartmentMaps => Set<HallDepartmentMap>();
        public DbSet<RemoteUserHallMap> RemoteUserHallMaps => Set<RemoteUserHallMap>();
        public DbSet<Facility> Facilities => Set<Facility>();
        public DbSet<HallFacilityMap> HallFacilityMaps => Set<HallFacilityMap>();
        public DbSet<VCPlatform> VCPlatforms => Set<VCPlatform>();
        public DbSet<HallVCPlatformMap> HallVCPlatformMaps => Set<HallVCPlatformMap>();

        // Booking.
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<BookingParticipant> BookingParticipants => Set<BookingParticipant>();
        public DbSet<BookingFacilityMap> BookingFacilityMaps => Set<BookingFacilityMap>();
        public DbSet<BookingVCDetails> BookingVCDetails => Set<BookingVCDetails>();

        // Reservations and modification holds.
        public DbSet<ReservationRecurrence> ReservationRecurrences => Set<ReservationRecurrence>();
        public DbSet<HallReservation> HallReservations => Set<HallReservation>();
        public DbSet<ReservationOccurrence> ReservationOccurrences => Set<ReservationOccurrence>();
        public DbSet<BookingModificationRequest> BookingModificationRequests => Set<BookingModificationRequest>();
        public DbSet<HallSlotHold> HallSlotHolds => Set<HallSlotHold>();

        // Remote availability.
        public DbSet<RemoteUserBusy> RemoteUserBusy => Set<RemoteUserBusy>();
        public DbSet<RemoteUserBusyHistory> RemoteUserBusyHistory => Set<RemoteUserBusyHistory>();

        // Meeting operations.
        public DbSet<MeetingOperation> MeetingOperations => Set<MeetingOperation>();
        public DbSet<MeetingAttendance> MeetingAttendances => Set<MeetingAttendance>();
        public DbSet<MeetingRecording> MeetingRecordings => Set<MeetingRecording>();

        // VC testing.
        public DbSet<VCTestSchedule> VCTestSchedules => Set<VCTestSchedule>();
        public DbSet<VCTestParticipant> VCTestParticipants => Set<VCTestParticipant>();

        // Audit and status history.
        public DbSet<BookingStatusHistory> BookingStatusHistory => Set<BookingStatusHistory>();
        public DbSet<BookingAuditLog> BookingAuditLogs => Set<BookingAuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigureSharedTables(modelBuilder);
            ConfigureBookingTables(modelBuilder);
        }

        private static void ConfigureSharedTables(ModelBuilder modelBuilder)
        {
            // Existing AuthAPI tables: map them, but do not let BookingAPI
            // migrations create, alter, or drop them.
            modelBuilder.Entity<UserProfile>(entity =>
            {
                entity.ToTable("Tbl_UserProfile", table => table.ExcludeFromMigrations());
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).ValueGeneratedOnAdd();
                entity.Property(x => x.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<Department>(entity =>
            {
                entity.ToTable("Tbl_Department", table => table.ExcludeFromMigrations());
                entity.HasKey(x => x.DepartmentId);
                entity.Property(x => x.DepartmentId).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<EmailNotificationOutbox>(entity =>
            {
                entity.ToTable("Tbl_EmailNotificationOutbox", table => table.ExcludeFromMigrations());
                entity.HasKey(x => x.EmailNotificationOutboxId);
                entity.Property(x => x.EmailNotificationOutboxId).ValueGeneratedOnAdd();
                entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
                entity.Property(x => x.ToEmail).HasMaxLength(256).IsRequired();
                entity.Property(x => x.CcEmail).HasMaxLength(2000);
                entity.Property(x => x.BccEmail).HasMaxLength(2000);
                entity.Property(x => x.Subject).HasMaxLength(500);
                entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
                entity.Property(x => x.Status).HasConversion<byte>();
                entity.Property(x => x.LockedBy).HasMaxLength(100);
                entity.Property(x => x.LastError).HasMaxLength(4000);
            });
        }

        private static void ConfigureBookingTables(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConferenceHall>(entity =>
            {
                entity.ToTable("Tbl_Master_ConferenceHall");
                entity.HasKey(x => x.ConferenceHallId);
                entity.Property(x => x.ConferenceHallId).UseIdentityColumn();
                entity.Property(x => x.HallCode).HasMaxLength(30).IsRequired();
                entity.Property(x => x.HallName).HasMaxLength(150).IsRequired();
                entity.Property(x => x.LocationName).HasMaxLength(150).IsRequired();
                entity.Property(x => x.Description).HasMaxLength(1000);
                entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.UpdatedByUserId).HasMaxLength(450);
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<HallManagerMap>(entity =>
            {
                entity.ToTable("Tbl_HallManagerMap");
                entity.HasKey(x => x.HallManagerMapId);
                entity.Property(x => x.HallManagerMapId).UseIdentityColumn();
                ConfigureUserAudit(entity.Property(x => x.UserId), entity.Property(x => x.CreatedByUserId));
                ConfigureDateTime(entity.Property(x => x.EffectiveFromUtc), entity.Property(x => x.EffectiveToUtc), entity.Property(x => x.CreatedAtUtc));
            });

            modelBuilder.Entity<HallSubAdminMap>(entity =>
            {
                entity.ToTable("Tbl_HallSubAdminMap");
                entity.HasKey(x => x.HallSubAdminMapId);
                entity.Property(x => x.HallSubAdminMapId).UseIdentityColumn();
                ConfigureUserAudit(entity.Property(x => x.UserId), entity.Property(x => x.CreatedByUserId));
                ConfigureDateTime(entity.Property(x => x.EffectiveFromUtc), entity.Property(x => x.EffectiveToUtc), entity.Property(x => x.CreatedAtUtc));
            });

            modelBuilder.Entity<HallDepartmentMap>(entity =>
            {
                entity.ToTable("Tbl_HallDepartmentMap");
                entity.HasKey(x => x.HallDepartmentMapId);
                entity.Property(x => x.HallDepartmentMapId).UseIdentityColumn();
                entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<RemoteUserHallMap>(entity =>
            {
                entity.ToTable("Tbl_RemoteUserHallMap");
                entity.HasKey(x => x.RemoteUserHallMapId);
                entity.Property(x => x.RemoteUserHallMapId).UseIdentityColumn();
                entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
                ConfigureDateTime(entity.Property(x => x.EffectiveFromUtc), entity.Property(x => x.EffectiveToUtc), entity.Property(x => x.CreatedAtUtc));
            });

            modelBuilder.Entity<Facility>(entity =>
            {
                entity.ToTable("Tbl_Master_Facility");
                entity.HasKey(x => x.FacilityId);
                entity.Property(x => x.FacilityId).UseIdentityColumn();
                entity.Property(x => x.FacilityCode).HasMaxLength(40).IsRequired();
                entity.Property(x => x.FacilityName).HasMaxLength(150).IsRequired();
                entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<HallFacilityMap>(entity =>
            {
                entity.ToTable("Tbl_HallFacilityMap");
                entity.HasKey(x => x.HallFacilityMapId);
                entity.Property(x => x.HallFacilityMapId).UseIdentityColumn();
                entity.Property(x => x.ConfigurationJson).HasColumnType("nvarchar(max)");
                entity.Property(x => x.Notes).HasMaxLength(500);
                entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<VCPlatform>(entity =>
            {
                entity.ToTable("Tbl_Master_VCPlatform");
                entity.HasKey(x => x.VCPlatformId);
                entity.Property(x => x.VCPlatformId).UseIdentityColumn();
                entity.Property(x => x.PlatformCode).HasMaxLength(40).IsRequired();
                entity.Property(x => x.PlatformName).HasMaxLength(150).IsRequired();
                entity.Property(x => x.Description).HasMaxLength(1000);
                entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<HallVCPlatformMap>(entity =>
            {
                entity.ToTable("Tbl_HallVCPlatformMap");
                entity.HasKey(x => x.HallVCPlatformMapId);
                entity.Property(x => x.HallVCPlatformMapId).UseIdentityColumn();
                entity.Property(x => x.ConfigurationJson).HasColumnType("nvarchar(max)");
                entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<Booking>(entity =>
            {
                entity.ToTable("Tbl_Booking_Header");
                entity.HasKey(x => x.BookingId);
                entity.Property(x => x.BookingId).UseIdentityColumn();
                entity.Property(x => x.BookingReferenceNo).HasMaxLength(30).IsRequired();
                entity.Property(x => x.RequestedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.ResponsiblePersonName).HasMaxLength(150).IsRequired();
                entity.Property(x => x.ResponsiblePersonDesignation).HasMaxLength(150).IsRequired();
                entity.Property(x => x.ResponsiblePersonMobile).HasMaxLength(15).IsRequired();
                entity.Property(x => x.MeetingSubject).HasMaxLength(250).IsRequired();
                entity.Property(x => x.ChairedBy).HasMaxLength(150).IsRequired();
                entity.Property(x => x.ApprovedByUserId).HasMaxLength(450);
                entity.Property(x => x.RejectionReason).HasMaxLength(1000);
                entity.Property(x => x.CancelledByUserId).HasMaxLength(450);
                entity.Property(x => x.CancellationReason).HasMaxLength(1000);
                entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.UpdatedByUserId).HasMaxLength(450);
                entity.Property(x => x.MeetingStartUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.MeetingEndUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.SubmittedAtUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.ApprovedAtUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.CancelledAtUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<BookingParticipant>(entity =>
            {
                entity.ToTable("Tbl_Booking_Participant");
                entity.HasKey(x => x.BookingParticipantId);
                entity.Property(x => x.BookingParticipantId).UseIdentityColumn();
                entity.Property(x => x.ParticipantUserId).HasMaxLength(450);
                entity.Property(x => x.ParticipantName).HasMaxLength(150).IsRequired();
                entity.Property(x => x.ParticipantDesignation).HasMaxLength(150);
                entity.Property(x => x.ParticipantEmail).HasMaxLength(256);
                entity.Property(x => x.AddedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.AddedAtUtc).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<BookingFacilityMap>(entity =>
            {
                entity.ToTable("Tbl_Booking_FacilityMap");
                entity.HasKey(x => x.BookingFacilityMapId);
                entity.Property(x => x.BookingFacilityMapId).UseIdentityColumn();
                entity.Property(x => x.Remarks).HasMaxLength(500);
            });

            modelBuilder.Entity<BookingVCDetails>(entity =>
            {
                entity.ToTable("Tbl_Booking_VCDetails");
                entity.HasKey(x => x.BookingVCDetailsId);
                entity.Property(x => x.BookingVCDetailsId).UseIdentityColumn();
                entity.Property(x => x.VCMeetingIdEncrypted).HasColumnType("varbinary(max)").IsRequired();
                entity.Property(x => x.VCPasswordEncrypted).HasColumnType("varbinary(max)").IsRequired();
                entity.Property(x => x.EncryptionKeyVersion).HasMaxLength(100).IsRequired();
                entity.Property(x => x.VCInstructions).HasMaxLength(1000);
                ConfigureDateTime(entity.Property(x => x.CreatedAtUtc), entity.Property(x => x.UpdatedAtUtc));
            });

            modelBuilder.Entity<ReservationRecurrence>(entity =>
            {
                entity.ToTable("Tbl_ReservationRecurrence");
                entity.HasKey(x => x.RecurrenceId);
                entity.Property(x => x.RecurrenceId).UseIdentityColumn();
                entity.Property(x => x.ReservedForUserId).HasMaxLength(450);
                entity.Property(x => x.StartDate).HasColumnType("date");
                entity.Property(x => x.EndDate).HasColumnType("date");
                entity.Property(x => x.StartTime).HasColumnType("time(0)");
                entity.Property(x => x.EndTime).HasColumnType("time(0)");
                entity.Property(x => x.ExpiryAtUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<HallReservation>(entity =>
            {
                entity.ToTable("Tbl_HallReservation");
                entity.HasKey(x => x.ReservationId);
                entity.Property(x => x.ReservationId).UseIdentityColumn();
                entity.Property(x => x.ReservedForUserId).HasMaxLength(450);
                entity.Property(x => x.ReservationStartUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.ReservationEndUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.ExpiryAtUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.Remarks).HasMaxLength(1000);
                entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
                ConfigureDateTime(entity.Property(x => x.CreatedAtUtc), entity.Property(x => x.UpdatedAtUtc));
            });

            modelBuilder.Entity<ReservationOccurrence>(entity =>
            {
                entity.ToTable("Tbl_ReservationOccurrence");
                entity.HasKey(x => x.ReservationOccurrenceId);
                entity.Property(x => x.ReservationOccurrenceId).UseIdentityColumn();
                entity.Property(x => x.ReservedForUserId).HasMaxLength(450);
                entity.Property(x => x.OccurrenceStartUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.OccurrenceEndUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<BookingModificationRequest>(entity =>
            {
                entity.ToTable("Tbl_Booking_ModificationRequest");
                entity.HasKey(x => x.ModificationRequestId);
                entity.Property(x => x.ModificationRequestId).UseIdentityColumn();
                entity.Property(x => x.RequestedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.ProposedContactName).HasMaxLength(150);
                entity.Property(x => x.ProposedContactDesignation).HasMaxLength(150);
                entity.Property(x => x.ProposedContactMobile).HasMaxLength(15);
                entity.Property(x => x.ProposedSubject).HasMaxLength(250);
                entity.Property(x => x.ProposedChairedBy).HasMaxLength(150);
                entity.Property(x => x.ProposedParticipantsJson).HasColumnType("nvarchar(max)");
                entity.Property(x => x.ProposedFacilitiesJson).HasColumnType("nvarchar(max)");
                entity.Property(x => x.ProposedVCDetailsEncrypted).HasColumnType("varbinary(max)");
                entity.Property(x => x.ChangeReason).HasMaxLength(1000).IsRequired();
                entity.Property(x => x.ReviewedByUserId).HasMaxLength(450);
                entity.Property(x => x.HoldExpiresAtUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.ReviewRemarks).HasMaxLength(1000);
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.ProposedStartUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.ProposedEndUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<HallSlotHold>(entity =>
            {
                entity.ToTable("Tbl_HallSlotHold");
                entity.HasKey(x => x.SlotHoldId);
                entity.Property(x => x.SlotHoldId).UseIdentityColumn();
                entity.Property(x => x.HoldStartUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.HoldEndUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.HoldExpiresAtUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.ReleasedAtUtc).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<RemoteUserBusy>(entity =>
            {
                entity.ToTable("Tbl_RemoteUserBusy");
                entity.HasKey(x => x.RemoteUserBusyId);
                entity.Property(x => x.RemoteUserBusyId).UseIdentityColumn();
                entity.Property(x => x.BusyReason).HasMaxLength(500);
                entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.RemovedByUserId).HasMaxLength(450);
                ConfigureDateTime(entity.Property(x => x.BusyStartUtc), entity.Property(x => x.BusyEndUtc),
                    entity.Property(x => x.CreatedAtUtc), entity.Property(x => x.RemovedAtUtc));
            });

            modelBuilder.Entity<RemoteUserBusyHistory>(entity =>
            {
                entity.ToTable("Tbl_RemoteUserBusyHistory");
                entity.HasKey(x => x.RemoteUserBusyHistoryId);
                entity.Property(x => x.RemoteUserBusyHistoryId).UseIdentityColumn();
                entity.Property(x => x.OldValuesJson).HasColumnType("nvarchar(max)");
                entity.Property(x => x.NewValuesJson).HasColumnType("nvarchar(max)");
                entity.Property(x => x.PerformedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.ChangedAtUtc).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<MeetingOperation>(entity =>
            {
                entity.ToTable("Tbl_Meeting_Operation");
                entity.HasKey(x => x.MeetingOperationId);
                entity.Property(x => x.MeetingOperationId).UseIdentityColumn();
                entity.Property(x => x.PerformedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.Remarks).HasMaxLength(1000);
                entity.Property(x => x.OperationAtUtc).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<MeetingAttendance>(entity =>
            {
                entity.ToTable("Tbl_Meeting_Attendance");
                entity.HasKey(x => x.MeetingAttendanceId);
                entity.Property(x => x.MeetingAttendanceId).UseIdentityColumn();
                entity.Property(x => x.MarkedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.MarkedAtUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.Remarks).HasMaxLength(500);
            });

            modelBuilder.Entity<MeetingRecording>(entity =>
            {
                entity.ToTable("Tbl_Meeting_Recording");
                entity.HasKey(x => x.MeetingRecordingId);
                entity.Property(x => x.MeetingRecordingId).UseIdentityColumn();
                entity.Property(x => x.RequestedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.MarkedReadyByUserId).HasMaxLength(450);
                entity.Property(x => x.CollectedByUserId).HasMaxLength(450);
                entity.Property(x => x.RequestedAtUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.MarkedReadyAtUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.CollectedAtUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.Remarks).HasMaxLength(1000);
            });

            modelBuilder.Entity<VCTestSchedule>(entity =>
            {
                entity.ToTable("Tbl_VCTestSchedule");
                entity.HasKey(x => x.VCTestScheduleId);
                entity.Property(x => x.VCTestScheduleId).UseIdentityColumn();
                entity.Property(x => x.TestSubject).HasMaxLength(250).IsRequired();
                entity.Property(x => x.ScheduledByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.ScheduledStartUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.ScheduledEndUtc).HasColumnType("datetime2(0)");
                entity.Property(x => x.Remarks).HasMaxLength(1000);
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<VCTestParticipant>(entity =>
            {
                entity.ToTable("Tbl_VCTestParticipant");
                entity.HasKey(x => x.VCTestParticipantId);
                entity.Property(x => x.VCTestParticipantId).UseIdentityColumn();
                entity.Property(x => x.ParticipantUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.ParticipantName).HasMaxLength(150).IsRequired();
                entity.Property(x => x.ParticipantEmail).HasMaxLength(256);
                entity.Property(x => x.Remarks).HasMaxLength(500);
            });

            modelBuilder.Entity<BookingStatusHistory>(entity =>
            {
                entity.ToTable("Tbl_Booking_StatusHistory");
                entity.HasKey(x => x.BookingStatusHistoryId);
                entity.Property(x => x.BookingStatusHistoryId).UseIdentityColumn();
                entity.Property(x => x.ChangedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.Remarks).HasMaxLength(1000);
                entity.Property(x => x.ChangedAtUtc).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<BookingAuditLog>(entity =>
            {
                entity.ToTable("Tbl_Booking_AuditLog");
                entity.HasKey(x => x.AuditLogId);
                entity.Property(x => x.AuditLogId).UseIdentityColumn();
                entity.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
                entity.Property(x => x.ActionType).HasMaxLength(80).IsRequired();
                entity.Property(x => x.PerformedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.OldValuesJson).HasColumnType("nvarchar(max)");
                entity.Property(x => x.NewValuesJson).HasColumnType("nvarchar(max)");
                entity.Property(x => x.Remarks).HasMaxLength(1000);
                entity.Property(x => x.IpAddress).HasMaxLength(50);
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(0)");
            });
        }

        private static void ConfigureUserAudit(
            Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<string> userId,
            Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<string> createdByUserId)
        {
            userId.HasMaxLength(450).IsRequired();
            createdByUserId.HasMaxLength(450).IsRequired();
        }

        private static void ConfigureDateTime(
            params Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<DateTime?>[] properties)
        {
            foreach (var property in properties)
                property.HasColumnType("datetime2(0)");
        }

        private static void ConfigureDateTime(
            Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<DateTime> requiredProperty,
            Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<DateTime?> nullableProperty,
            Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<DateTime> anotherRequiredProperty)
        {
            requiredProperty.HasColumnType("datetime2(0)");
            nullableProperty.HasColumnType("datetime2(0)");
            anotherRequiredProperty.HasColumnType("datetime2(0)");
        }

        private static void ConfigureDateTime(
            Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<DateTime> requiredProperty,
            Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<DateTime?> nullableProperty)
        {
            requiredProperty.HasColumnType("datetime2(0)");
            nullableProperty.HasColumnType("datetime2(0)");
        }

        private static void ConfigureDateTime(
            Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<DateTime> firstRequiredProperty,
            Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<DateTime> secondRequiredProperty,
            Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<DateTime> thirdRequiredProperty,
            Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<DateTime?> nullableProperty)
        {
            firstRequiredProperty.HasColumnType("datetime2(0)");
            secondRequiredProperty.HasColumnType("datetime2(0)");
            thirdRequiredProperty.HasColumnType("datetime2(0)");
            nullableProperty.HasColumnType("datetime2(0)");
        }
    }
}
