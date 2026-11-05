using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.BookingAPI.Models.Dto
{
    // Common authenticated actor context is derived from JWT claims on the server.
    // Never accept PerformedByUserId / CreatedByUserId from an ordinary client DTO.

    public class ConferenceHallDto
    {
        public int ConferenceHallId { get; set; }
        public string HallCode { get; set; } = string.Empty;
        public string HallName { get; set; } = string.Empty;
        public byte LocationType { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public int? Capacity { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public byte[] RowVersion { get; set; } = null!;
        public List<FacilityDto> Facilities { get; set; } = new();
        public List<VCPlatformDto> VCPlatforms { get; set; } = new();
    }

    public class CreateHallDto
    {
        [Required, StringLength(30)] public string HallCode { get; set; } = string.Empty;
        [Required, StringLength(150)] public string HallName { get; set; } = string.Empty;
        [Range(1, 2)] public byte LocationType { get; set; }
        [Required, StringLength(150)] public string LocationName { get; set; } = string.Empty;
        [Range(1, int.MaxValue)] public int? Capacity { get; set; }
        [StringLength(1000)] public string? Description { get; set; }
    }


    public class UpdateHallDto : CreateHallDto
    {
        [Required]
        public int ConferenceHallId { get; set; }

        public bool IsActive { get; set; }

        [Required]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }


    public class HallUserMappingDto
    {
        [Required] public int ConferenceHallId { get; set; }
        [Required] public string UserId { get; set; } = string.Empty;
        [Required] public DateTime EffectiveFromUtc { get; set; }
        public DateTime? EffectiveToUtc { get; set; }
        public bool IsPrimary { get; set; } = true;
    }

    public class HallDepartmentMappingDto
    {
        [Required] public int ConferenceHallId { get; set; }
        [Required] public int DepartmentId { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class FacilityDto
    {
        public int FacilityId { get; set; }
        public string FacilityCode { get; set; } = string.Empty;
        public string FacilityName { get; set; } = string.Empty;
        public byte FacilityCategory { get; set; }
        public bool RequiresParticipantSelection { get; set; }
        public bool RequiresVCPlatform { get; set; }
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsAvailableInHall { get; set; }
        public string? ConfigurationJson { get; set; }
    }

    public class CreateFacilityDto
    {
        [Required, StringLength(40)] public string FacilityCode { get; set; } = string.Empty;
        [Required, StringLength(150)] public string FacilityName { get; set; } = string.Empty;
        [Range(1, 4)] public byte FacilityCategory { get; set; }
        public bool RequiresParticipantSelection { get; set; }
        public bool RequiresVCPlatform { get; set; }
        public int DisplayOrder { get; set; }
    }


    public class UpdateFacilityDto : CreateFacilityDto
    {
        [Required]
        public int FacilityId { get; set; }

        public bool IsActive { get; set; }
    }


    public class HallFacilitySelectionDto
    {
        [Required] public int FacilityId { get; set; }
        public bool IsAvailable { get; set; } = true;
        public string? ConfigurationJson { get; set; }
        [StringLength(500)] public string? Notes { get; set; }
    }

    public class VCPlatformDto
    {
        public int VCPlatformId { get; set; }
        public string PlatformCode { get; set; } = string.Empty;
        public string PlatformName { get; set; } = string.Empty;
        public byte NetworkType { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public bool IsAvailableInHall { get; set; }
        public string? ConfigurationJson { get; set; }
    }

    public class CreateVCPlatformDto
    {
        [Required, StringLength(40)] public string PlatformCode { get; set; } = string.Empty;
        [Required, StringLength(150)] public string PlatformName { get; set; } = string.Empty;
        [Range(1, 2)] public byte NetworkType { get; set; }
        [StringLength(1000)] public string? Description { get; set; }
    }

    public class UpdateVCPlatformDto : CreateVCPlatformDto
    {
        [Required]
        public int VCPlatformId { get; set; }

        public bool IsActive { get; set; }
    }

    public class HallVCPlatformSelectionDto
    {
        [Required] public int VCPlatformId { get; set; }
        public bool IsActive { get; set; } = true;
        public string? ConfigurationJson { get; set; }
    }

    public class BookingParticipantDto
    {
        public string? ParticipantUserId { get; set; }
        [Required, StringLength(150)] public string ParticipantName { get; set; } = string.Empty;
        [StringLength(150)] public string? ParticipantDesignation { get; set; }
        public int? ParticipantDepartmentId { get; set; }
        [EmailAddress, StringLength(256)] public string? ParticipantEmail { get; set; }
        [Range(1, 3)] public byte ParticipantType { get; set; }
        public bool IsRequired { get; set; } = true;
    }

    public class BookingFacilityDto
    {
        [Required] public int FacilityId { get; set; }
        public bool IsRequested { get; set; } = true;
        [StringLength(500)] public string? Remarks { get; set; }
    }

    // Credentials are accepted only over HTTPS and encrypted by the service before persistence.
    // They must never be logged or returned in a normal booking response.
    public class BookingVCCredentialsDto
    {
        [Required, StringLength(500)] public string VCMeetingId { get; set; } = string.Empty;
        [Required, StringLength(500)] public string VCPassword { get; set; } = string.Empty;
        [StringLength(1000)] public string? VCInstructions { get; set; }
    }

    public class CreateBookingDto
    {
        [Required] public int ConferenceHallId { get; set; }
        [Required] public int BookingDepartmentId { get; set; }
        [Required, StringLength(150)] public string ResponsiblePersonName { get; set; } = string.Empty;
        [Required, StringLength(150)] public string ResponsiblePersonDesignation { get; set; } = string.Empty;
        [Required, Phone, StringLength(15)] public string ResponsiblePersonMobile { get; set; } = string.Empty;
        [Required, StringLength(250)] public string MeetingSubject { get; set; } = string.Empty;
        [Required, StringLength(150)] public string ChairedBy { get; set; } = string.Empty;
        [Required] public DateTime MeetingStartUtc { get; set; }
        [Required] public DateTime MeetingEndUtc { get; set; }
        public short CoolingMinutes { get; set; } = 15;
        public long? ReservationOccurrenceId { get; set; }
        public List<BookingParticipantDto> Participants { get; set; } = new();
        public List<BookingFacilityDto> Facilities { get; set; } = new();
        public int? VCPlatformId { get; set; }
        public BookingVCCredentialsDto? VCCredentials { get; set; }
    }

    public class BookingListItemDto
    {
        public long BookingId { get; set; }
        public string BookingReferenceNo { get; set; } = string.Empty;
        public int ConferenceHallId { get; set; }
        public string HallName { get; set; } = string.Empty;
        public int BookingDepartmentId { get; set; }
        public string BookingDepartmentName { get; set; } = string.Empty;
        public string RequestedByUserId { get; set; } = string.Empty;
        public string ResponsiblePersonName { get; set; } = string.Empty;
        public string ResponsiblePersonDesignation { get; set; } = string.Empty;
        public string ResponsiblePersonMobile { get; set; } = string.Empty;
        public string MeetingSubject { get; set; } = string.Empty;
        public string ChairedBy { get; set; } = string.Empty;
        public DateTime MeetingStartUtc { get; set; }
        public DateTime MeetingEndUtc { get; set; }
        public byte BookingType { get; set; }
        public byte ApprovalStatus { get; set; }
        public byte MeetingStatus { get; set; }
        public bool IsCurrentUserParticipant { get; set; }
    }

    public class BookingDetailsDto : BookingListItemDto
    {
        public short CoolingMinutes { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
        public string? RejectionReason { get; set; }
        public string? CancellationReason { get; set; }
        public List<BookingParticipantViewDto> Participants { get; set; } = new();
        public List<BookingFacilityDto> Facilities { get; set; } = new();
        public int? VCPlatformId { get; set; }
        public string? VCPlatformName { get; set; }
        public string? VCInstructions { get; set; }
        // Credentials are intentionally omitted. A separately authorized reveal workflow
        // should be used if operational policy requires retrieval.
    }

    public class BookingParticipantViewDto : BookingParticipantDto
    {
        public long BookingParticipantId { get; set; }
        public byte AttendanceStatus { get; set; }
    }

    public class BookingSearchDto
    {
        public int? ConferenceHallId { get; set; }
        public int? BookingDepartmentId { get; set; }
        public string? RequestedByUserId { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public byte? ApprovalStatus { get; set; }
        public byte? MeetingStatus { get; set; }
        public string? SearchText { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class PagedResultDto<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalRecords / PageSize);
    }

    public class BookingDecisionDto
    {
        [Required] public long BookingId { get; set; }
        [StringLength(1000)] public string? Remarks { get; set; }
    }

    public class RejectBookingDto : BookingDecisionDto
    {
        [Required, StringLength(1000)] public string RejectionReason { get; set; } = string.Empty;
    }

    public class CancelBookingDto
    {
        [Required] public long BookingId { get; set; }
        [Required, StringLength(1000)] public string CancellationReason { get; set; } = string.Empty;
    }

    public class BookingCalendarQueryDto
    {
        [Required] public DateTime FromUtc { get; set; }
        [Required] public DateTime ToUtc { get; set; }
        public List<int>? ConferenceHallIds { get; set; }
    }

    public class BookingCalendarItemDto
    {
        public long? BookingId { get; set; }
        public string? BookingReferenceNo { get; set; }
        public int ConferenceHallId { get; set; }
        public string HallName { get; set; } = string.Empty;
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public string DisplayTitle { get; set; } = string.Empty;
        public string? BookingDepartmentName { get; set; }
        public string? Subject { get; set; }
        public string? ContactName { get; set; }
        public string? ContactDesignation { get; set; }
        public string? ContactMobile { get; set; }
        public byte? ApprovalStatus { get; set; }
        public bool IsReservationBlock { get; set; }
        // Reservation owner and private reservation details are never sent to non-owners.
    }

    public class ReservationCreateDto
    {
        [Required] public int ConferenceHallId { get; set; }
        public string? ReservedForUserId { get; set; }
        public int? ReservedForDepartmentId { get; set; }
        [Required] public DateTime ReservationStartUtc { get; set; }
        [Required] public DateTime ReservationEndUtc { get; set; }
        public bool IsIndefinite { get; set; }
        public DateTime? ExpiryAtUtc { get; set; }
        [StringLength(1000)] public string? Remarks { get; set; }
    }

    public class ReservationDto : ReservationCreateDto
    {
        public long ReservationId { get; set; }
        public byte ReservationStatus { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class RecurrenceCreateDto
    {
        [Required] public int ConferenceHallId { get; set; }
        public string? ReservedForUserId { get; set; }
        public int? ReservedForDepartmentId { get; set; }
        [Required] public DateTime StartDate { get; set; }
        [Required] public DateTime EndDate { get; set; }
        [Required] public TimeSpan StartTime { get; set; }
        [Required] public TimeSpan EndTime { get; set; }
        [Range(1, 3)] public byte RecurrencePattern { get; set; }
        public byte? DaysOfWeekMask { get; set; }
        public byte? DayOfMonth { get; set; }
        public DateTime? ExpiryAtUtc { get; set; }
        public bool IsIndefinite { get; set; }
    }

    public class ReservationOccurrenceDto
    {
        public long ReservationOccurrenceId { get; set; }
        public long RecurrenceId { get; set; }
        public int ConferenceHallId { get; set; }
        public DateTime OccurrenceStartUtc { get; set; }
        public DateTime OccurrenceEndUtc { get; set; }
        public byte OccurrenceStatus { get; set; }
        public long? BookingId { get; set; }
    }

    public class BookingModificationDto
    {
        [Required] public long BookingId { get; set; }
        [Required] public int ProposedHallId { get; set; }
        [Required] public DateTime ProposedStartUtc { get; set; }
        [Required] public DateTime ProposedEndUtc { get; set; }
        [StringLength(250)] public string? ProposedSubject { get; set; }
        [StringLength(150)] public string? ProposedChairedBy { get; set; }
        [StringLength(150)] public string? ProposedContactName { get; set; }
        [StringLength(150)] public string? ProposedContactDesignation { get; set; }
        [Phone, StringLength(15)] public string? ProposedContactMobile { get; set; }
        public List<BookingParticipantDto>? ProposedParticipants { get; set; }
        public List<BookingFacilityDto>? ProposedFacilities { get; set; }
        public int? ProposedVCPlatformId { get; set; }
        public BookingVCCredentialsDto? ProposedVCCredentials { get; set; }
        [Required, StringLength(1000)] public string ChangeReason { get; set; } = string.Empty;
    }

    public class ModificationReviewDto
    {
        [Required] public long ModificationRequestId { get; set; }
        [StringLength(1000)] public string? ReviewRemarks { get; set; }
    }

    public class ModificationRequestViewDto
    {
        public long ModificationRequestId { get; set; }
        public long BookingId { get; set; }
        public string RequestedByUserId { get; set; } = string.Empty;
        public int RequestedByDepartmentId { get; set; }
        public int ProposedHallId { get; set; }
        public DateTime ProposedStartUtc { get; set; }
        public DateTime ProposedEndUtc { get; set; }
        public string? ProposedSubject { get; set; }
        public string ChangeReason { get; set; } = string.Empty;
        public byte ModificationStatus { get; set; }
        public DateTime HoldExpiresAtUtc { get; set; }
        public string? ReviewRemarks { get; set; }
    }

    public class RemoteBusyCreateDto
    {
        [Required] public DateTime BusyStartUtc { get; set; }
        [Required] public DateTime BusyEndUtc { get; set; }
        [StringLength(500)] public string? BusyReason { get; set; }
    }

    public class RemoteBusyDto
    {
        public long RemoteUserBusyId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime BusyStartUtc { get; set; }
        public DateTime BusyEndUtc { get; set; }
        public string? BusyReason { get; set; }
        public byte BusySource { get; set; }
        public bool IsActive { get; set; }
    }

    public class RemoteParticipantAvailabilityDto
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsBusy { get; set; }
        public bool HasAnotherBooking { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    public class MeetingOperationDto
    {
        [Required] public long BookingId { get; set; }
        [StringLength(1000)] public string? Remarks { get; set; }
    }

    public class MeetingAttendanceUpdateDto
    {
        [Required] public long BookingParticipantId { get; set; }
        [Range(1, 2)] public byte AttendanceStatus { get; set; }
        [StringLength(500)] public string? Remarks { get; set; }
    }

    public class RecordingRequestDto
    {
        [Required] public long BookingId { get; set; }
        [StringLength(1000)] public string? Remarks { get; set; }
    }

    public class RecordingStatusUpdateDto
    {
        [Required] public long MeetingRecordingId { get; set; }
        [Range(1, 5)] public byte RecordingStatus { get; set; }
        [StringLength(1000)] public string? Remarks { get; set; }
    }

    public class RecordingDto
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

    public class VCTestCreateDto
    {
        [Required] public int ConferenceHallId { get; set; }
        [Required] public int VCPlatformId { get; set; }
        [Required, StringLength(250)] public string TestSubject { get; set; } = string.Empty;
        [Required] public DateTime ScheduledStartUtc { get; set; }
        [Required] public DateTime ScheduledEndUtc { get; set; }
        public List<VCTestParticipantDto> Participants { get; set; } = new();
    }

    public class VCTestParticipantDto
    {
        [Required] public string ParticipantUserId { get; set; } = string.Empty;
        [Required, StringLength(150)] public string ParticipantName { get; set; } = string.Empty;
        [EmailAddress, StringLength(256)] public string? ParticipantEmail { get; set; }
    }

    public class VCTestResultUpdateDto
    {
        [Required] public long VCTestScheduleId { get; set; }
        [Range(1, 3)] public byte TestResult { get; set; }
        [StringLength(1000)] public string? Remarks { get; set; }
    }

    public class VCTestViewDto
    {
        public long VCTestScheduleId { get; set; }
        public long BookingId { get; set; }
        public int VCPlatformId { get; set; }
        public string TestSubject { get; set; } = string.Empty;
        public DateTime ScheduledStartUtc { get; set; }
        public DateTime ScheduledEndUtc { get; set; }
        public byte TestStatus { get; set; }
        public byte? TestResult { get; set; }
        public string? Remarks { get; set; }
        public List<VCTestParticipantDto> Participants { get; set; } = new();
    }

    public class BookingStatusHistoryDto
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

    public class BookingAuditLogDto
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
        public DateTime CreatedAtUtc { get; set; }
    }

    public class ApiResultDto<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
