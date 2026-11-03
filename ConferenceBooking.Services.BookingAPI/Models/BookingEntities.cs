using System;

namespace ConferenceBooking.Services.BookingAPI.Models
{
    public class ConferenceHall
    {
        public int ConferenceHallId { get; set; }
        public string HallCode { get; set; } = string.Empty;
        public string HallName { get; set; } = string.Empty;
        public byte LocationType { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public int? Capacity { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime? UpdatedAtUtc { get; set; }
        public string? UpdatedByUserId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public class HallManagerMap
    {
        public long HallManagerMapId { get; set; }
        public int ConferenceHallId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime EffectiveFromUtc { get; set; }
        public DateTime? EffectiveToUtc { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
    }

    public class HallSubAdminMap
    {
        public long HallSubAdminMapId { get; set; }
        public int ConferenceHallId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime EffectiveFromUtc { get; set; }
        public DateTime? EffectiveToUtc { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
    }

    public class HallDepartmentMap
    {
        public long HallDepartmentMapId { get; set; }
        public int ConferenceHallId { get; set; }
        public int DepartmentId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
    }

    public class RemoteUserHallMap
    {
        public long RemoteUserHallMapId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int ConferenceHallId { get; set; }
        public DateTime EffectiveFromUtc { get; set; }
        public DateTime? EffectiveToUtc { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
    }

    public class Facility
    {
        public int FacilityId { get; set; }
        public string FacilityCode { get; set; } = string.Empty;
        public string FacilityName { get; set; } = string.Empty;
        public byte FacilityCategory { get; set; }
        public bool RequiresParticipantSelection { get; set; }
        public bool RequiresVCPlatform { get; set; }
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
    }

    public class HallFacilityMap
    {
        public long HallFacilityMapId { get; set; }
        public int ConferenceHallId { get; set; }
        public int FacilityId { get; set; }
        public bool IsAvailable { get; set; }
        public string? ConfigurationJson { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
    }

    public class VCPlatform
    {
        public int VCPlatformId { get; set; }
        public string PlatformCode { get; set; } = string.Empty;
        public string PlatformName { get; set; } = string.Empty;
        public byte NetworkType { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
    }

    public class HallVCPlatformMap
    {
        public long HallVCPlatformMapId { get; set; }
        public int ConferenceHallId { get; set; }
        public int VCPlatformId { get; set; }
        public bool IsActive { get; set; }
        public string? ConfigurationJson { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
    }

    public class Booking
    {
        public long BookingId { get; set; }
        public string BookingReferenceNo { get; set; } = string.Empty;
        public int ConferenceHallId { get; set; }
        public int BookingDepartmentId { get; set; }
        public string RequestedByUserId { get; set; } = string.Empty;
        public string ResponsiblePersonName { get; set; } = string.Empty;
        public string ResponsiblePersonDesignation { get; set; } = string.Empty;
        public string ResponsiblePersonMobile { get; set; } = string.Empty;
        public string MeetingSubject { get; set; } = string.Empty;
        public string ChairedBy { get; set; } = string.Empty;
        public DateTime MeetingStartUtc { get; set; }
        public DateTime MeetingEndUtc { get; set; }
        public short CoolingMinutes { get; set; }
        public byte BookingType { get; set; }
        public byte ApprovalStatus { get; set; }
        public byte MeetingStatus { get; set; }
        public long? ReservationOccurrenceId { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
        public string? ApprovedByUserId { get; set; }
        public DateTime? ApprovedAtUtc { get; set; }
        public string? RejectionReason { get; set; }
        public string? CancelledByUserId { get; set; }
        public DateTime? CancelledAtUtc { get; set; }
        public string? CancellationReason { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime? UpdatedAtUtc { get; set; }
        public string? UpdatedByUserId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public class BookingParticipant
    {
        public long BookingParticipantId { get; set; }
        public long BookingId { get; set; }
        public string? ParticipantUserId { get; set; }
        public string ParticipantName { get; set; } = string.Empty;
        public string? ParticipantDesignation { get; set; }
        public int? ParticipantDepartmentId { get; set; }
        public string? ParticipantEmail { get; set; }
        public byte ParticipantType { get; set; }
        public bool IsRequired { get; set; }
        public byte AttendanceStatus { get; set; }
        public string AddedByUserId { get; set; } = string.Empty;
        public DateTime AddedAtUtc { get; set; }
        public bool IsActive { get; set; }
    }

    public class BookingFacilityMap
    {
        public long BookingFacilityMapId { get; set; }
        public long BookingId { get; set; }
        public int FacilityId { get; set; }
        public bool IsRequested { get; set; }
        public string? Remarks { get; set; }
    }

    public class BookingVCDetails
    {
        public long BookingVCDetailsId { get; set; }
        public long BookingId { get; set; }
        public int VCPlatformId { get; set; }
        public byte[] VCMeetingIdEncrypted { get; set; } = Array.Empty<byte>();
        public byte[] VCPasswordEncrypted { get; set; } = Array.Empty<byte>();
        public string EncryptionKeyVersion { get; set; } = string.Empty;
        public string? VCInstructions { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }

    public class ReservationRecurrence
    {
        public long RecurrenceId { get; set; }
        public int ConferenceHallId { get; set; }
        public string? ReservedForUserId { get; set; }
        public int? ReservedForDepartmentId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public byte RecurrencePattern { get; set; }
        public byte? DaysOfWeekMask { get; set; }
        public byte? DayOfMonth { get; set; }
        public DateTime? ExpiryAtUtc { get; set; }
        public bool IsIndefinite { get; set; }
        public bool IsActive { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    public class HallReservation
    {
        public long ReservationId { get; set; }
        public int ConferenceHallId { get; set; }
        public string? ReservedForUserId { get; set; }
        public int? ReservedForDepartmentId { get; set; }
        public DateTime ReservationStartUtc { get; set; }
        public DateTime ReservationEndUtc { get; set; }
        public long? RecurrenceId { get; set; }
        public long? ReservationOccurrenceId { get; set; }
        public byte ReservationStatus { get; set; }
        public bool IsIndefinite { get; set; }
        public DateTime? ExpiryAtUtc { get; set; }
        public string? Remarks { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }

    public class ReservationOccurrence
    {
        public long ReservationOccurrenceId { get; set; }
        public long RecurrenceId { get; set; }
        public int ConferenceHallId { get; set; }
        public string? ReservedForUserId { get; set; }
        public int? ReservedForDepartmentId { get; set; }
        public DateTime OccurrenceStartUtc { get; set; }
        public DateTime OccurrenceEndUtc { get; set; }
        public byte OccurrenceStatus { get; set; }
        public long? BookingId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class BookingModificationRequest
    {
        public long ModificationRequestId { get; set; }
        public long BookingId { get; set; }
        public string RequestedByUserId { get; set; } = string.Empty;
        public int RequestedByDepartmentId { get; set; }
        public int ProposedHallId { get; set; }
        public DateTime ProposedStartUtc { get; set; }
        public DateTime ProposedEndUtc { get; set; }
        public string? ProposedSubject { get; set; }
        public string? ProposedChairedBy { get; set; }
        public string? ProposedContactName { get; set; }
        public string? ProposedContactDesignation { get; set; }
        public string? ProposedContactMobile { get; set; }
        public string? ProposedParticipantsJson { get; set; }
        public string? ProposedFacilitiesJson { get; set; }
        public byte[]? ProposedVCDetailsEncrypted { get; set; }
        public string ChangeReason { get; set; } = string.Empty;
        public byte ModificationStatus { get; set; }
        public DateTime HoldExpiresAtUtc { get; set; }
        public string? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
        public string? ReviewRemarks { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public class HallSlotHold
    {
        public long SlotHoldId { get; set; }
        public int ConferenceHallId { get; set; }
        public long BookingId { get; set; }
        public long ModificationRequestId { get; set; }
        public DateTime HoldStartUtc { get; set; }
        public DateTime HoldEndUtc { get; set; }
        public DateTime HoldExpiresAtUtc { get; set; }
        public byte HoldStatus { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ReleasedAtUtc { get; set; }
    }

    public class RemoteUserBusy
    {
        public long RemoteUserBusyId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime BusyStartUtc { get; set; }
        public DateTime BusyEndUtc { get; set; }
        public string? BusyReason { get; set; }
        public byte BusySource { get; set; }
        public bool IsActive { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public string? RemovedByUserId { get; set; }
        public DateTime? RemovedAtUtc { get; set; }
    }

    public class RemoteUserBusyHistory
    {
        public long RemoteUserBusyHistoryId { get; set; }
        public long RemoteUserBusyId { get; set; }
        public byte ActionType { get; set; }
        public string? OldValuesJson { get; set; }
        public string? NewValuesJson { get; set; }
        public string PerformedByUserId { get; set; } = string.Empty;
        public int? RepresentedDepartmentId { get; set; }
        public DateTime ChangedAtUtc { get; set; }
    }

    public class MeetingOperation
    {
        public long MeetingOperationId { get; set; }
        public long BookingId { get; set; }
        public byte OperationType { get; set; }
        public DateTime OperationAtUtc { get; set; }
        public string PerformedByUserId { get; set; } = string.Empty;
        public int? RepresentedDepartmentId { get; set; }
        public string? Remarks { get; set; }
    }

    public class MeetingAttendance
    {
        public long MeetingAttendanceId { get; set; }
        public long BookingId { get; set; }
        public long BookingParticipantId { get; set; }
        public byte AttendanceStatus { get; set; }
        public string MarkedByUserId { get; set; } = string.Empty;
        public DateTime MarkedAtUtc { get; set; }
        public string? Remarks { get; set; }
    }

    public class MeetingRecording
    {
        public long MeetingRecordingId { get; set; }
        public long BookingId { get; set; }
        public string RequestedByUserId { get; set; } = string.Empty;
        public DateTime RequestedAtUtc { get; set; }
        public byte RecordingStatus { get; set; }
        public string? MarkedReadyByUserId { get; set; }
        public DateTime? MarkedReadyAtUtc { get; set; }
        public string? CollectedByUserId { get; set; }
        public DateTime? CollectedAtUtc { get; set; }
        public string? Remarks { get; set; }
    }

    public class VCTestSchedule
    {
        public long VCTestScheduleId { get; set; }
        public long BookingId { get; set; }
        public int VCPlatformId { get; set; }
        public string TestSubject { get; set; } = string.Empty;
        public DateTime ScheduledStartUtc { get; set; }
        public DateTime ScheduledEndUtc { get; set; }
        public string ScheduledByUserId { get; set; } = string.Empty;
        public byte TestStatus { get; set; }
        public byte? TestResult { get; set; }
        public string? Remarks { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class VCTestParticipant
    {
        public long VCTestParticipantId { get; set; }
        public long VCTestScheduleId { get; set; }
        public string ParticipantUserId { get; set; } = string.Empty;
        public string ParticipantName { get; set; } = string.Empty;
        public string? ParticipantEmail { get; set; }
        public byte AttendanceStatus { get; set; }
        public string? Remarks { get; set; }
    }

    public class BookingStatusHistory
    {
        public long BookingStatusHistoryId { get; set; }
        public long BookingId { get; set; }
        public byte? OldApprovalStatus { get; set; }
        public byte? NewApprovalStatus { get; set; }
        public byte? OldMeetingStatus { get; set; }
        public byte? NewMeetingStatus { get; set; }
        public string ChangedByUserId { get; set; } = string.Empty;
        public int? RepresentedDepartmentId { get; set; }
        public string? Remarks { get; set; }
        public DateTime ChangedAtUtc { get; set; }
    }

    public class BookingAuditLog
    {
        public long AuditLogId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public long EntityId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string PerformedByUserId { get; set; } = string.Empty;
        public int? RepresentedDepartmentId { get; set; }
        public string? OldValuesJson { get; set; }
        public string? NewValuesJson { get; set; }
        public string? Remarks { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
