using System.Data;
using System.Text;
using ConferenceBooking.Services.BookingAPI.Data;
using ConferenceBooking.Services.BookingAPI.Enums;
using ConferenceBooking.Services.BookingAPI.Models;
using ConferenceBooking.Services.BookingAPI.Models.Dto;
using ConferenceBooking.Services.BookingAPI.Service.IService;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ConferenceBooking.Services.BookingAPI.Service
{
    public class BookingService : IBookingService
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUser;

        private const short DefaultCoolingMinutes = 15;

        public BookingService(
            ApplicationDbContext db,
            ICurrentUserService currentUser)
        {
            _db = db;
            _currentUser = currentUser;
        }

        public async Task<ResponseDto> CreateBooking(
            CreateBookingDto dto,
            CancellationToken cancellationToken = default)
        {
            var response = new ResponseDto();

            try
            {
                // 1. Authenticated actor.
                if (!_currentUser.IsAuthenticated ||
                    string.IsNullOrWhiteSpace(_currentUser.UserId))
                {
                    return Failure(401, "Authentication is required.");
                }

                var actorId = _currentUser.UserId;

                var profile = await _db.UserProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.UserId == actorId && x.IsActive,
                        cancellationToken);

                if (profile == null || !profile.DepartmentId.HasValue)
                    return Failure(403, "Active user profile or department not found.");

                // Initial policy: users can book on behalf of their own department.
                if (profile.DepartmentId.Value != dto.BookingDepartmentId)
                    return Failure(403, "You cannot submit a booking for this department.");

                var department = await _db.Departments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.DepartmentId == dto.BookingDepartmentId &&
                             x.IsActive,
                        cancellationToken);

                if (department == null)
                    return Failure(400, "Invalid or inactive booking department.");

                // 2. Validate request fields.
                if (dto.MeetingStartUtc.Kind == DateTimeKind.Local ||
                    dto.MeetingEndUtc.Kind == DateTimeKind.Local)
                {
                    return Failure(400, "Meeting dates must be supplied in UTC.");
                }

                var startUtc = DateTime.SpecifyKind(
                    dto.MeetingStartUtc, DateTimeKind.Utc);

                var endUtc = DateTime.SpecifyKind(
                    dto.MeetingEndUtc, DateTimeKind.Utc);

                if (startUtc <= DateTime.UtcNow)
                    return Failure(400, "Meeting start time must be in the future.");

                if (endUtc <= startUtc)
                    return Failure(400, "Meeting end time must be later than start time.");

                if (dto.CoolingMinutes < 0 || dto.CoolingMinutes > 120)
                    return Failure(400, "Cooling time must be between 0 and 120 minutes.");

                var coolingMinutes = dto.CoolingMinutes == 0
                    ? DefaultCoolingMinutes
                    : dto.CoolingMinutes;

                var occupiedEndUtc = endUtc.AddMinutes(coolingMinutes);

                if (string.IsNullOrWhiteSpace(dto.ResponsiblePersonName) ||
                    string.IsNullOrWhiteSpace(dto.ResponsiblePersonDesignation) ||
                    string.IsNullOrWhiteSpace(dto.ResponsiblePersonMobile) ||
                    string.IsNullOrWhiteSpace(dto.MeetingSubject) ||
                    string.IsNullOrWhiteSpace(dto.ChairedBy))
                {
                    return Failure(400, "Contact details, meeting subject and chaired-by are compulsory.");
                }

                // 3. Validate hall.
                var hallExists = await _db.ConferenceHalls
                    .AnyAsync(
                        x => x.ConferenceHallId == dto.ConferenceHallId &&
                             x.IsActive,
                        cancellationToken);

                if (!hallExists)
                    return Failure(404, "Conference hall not found or inactive.");

                // 4. Validate facilities.
                var facilityIds = dto.Facilities
                    .Where(x => x.IsRequested)
                    .Select(x => x.FacilityId)
                    .Distinct()
                    .ToList();

                if (facilityIds.Count != dto.Facilities.Count(x => x.IsRequested))
                    return Failure(400, "Duplicate facility selections are not allowed.");

                var hallFacilities = await (
                    from map in _db.HallFacilityMaps
                    join facility in _db.Facilities
                        on map.FacilityId equals facility.FacilityId
                    where map.ConferenceHallId == dto.ConferenceHallId
                          && map.IsAvailable
                          && facility.IsActive
                          && facilityIds.Contains(facility.FacilityId)
                    select facility
                ).ToListAsync(cancellationToken);

                if (hallFacilities.Count != facilityIds.Count)
                    return Failure(400, "One or more selected facilities are not available in this hall.");

                var requiresParticipants =
                    hallFacilities.Any(x => x.RequiresParticipantSelection);

                // 5. Validate VC platform.
                VCPlatform? selectedPlatform = null;

                if (dto.VCPlatformId.HasValue)
                {
                    selectedPlatform = await (
                        from platform in _db.VCPlatforms
                        join map in _db.HallVCPlatformMaps
                            on platform.VCPlatformId equals map.VCPlatformId
                        where platform.VCPlatformId == dto.VCPlatformId.Value
                              && platform.IsActive
                              && map.ConferenceHallId == dto.ConferenceHallId
                              && map.IsActive
                        select platform
                    ).FirstOrDefaultAsync(cancellationToken);

                    if (selectedPlatform == null)
                        return Failure(400, "Selected VC platform is not available in this hall.");
                }

                if (hallFacilities.Any(x => x.RequiresVCPlatform) &&
                    !dto.VCPlatformId.HasValue)
                {
                    return Failure(400, "A VC platform is required for the selected facility.");
                }

                if (dto.VCPlatformId.HasValue)
                {
                    if (dto.VCCredentials == null ||
                        string.IsNullOrWhiteSpace(dto.VCCredentials.VCMeetingId) ||
                        string.IsNullOrWhiteSpace(dto.VCCredentials.VCPassword))
                    {
                        return Failure(400, "VC Meeting ID and password are compulsory.");
                    }
                }
                else if (dto.VCCredentials != null)
                {
                    return Failure(400, "VC credentials cannot be supplied without selecting a VC platform.");
                }

                // Intranet VC requires participant selection.
                if (selectedPlatform?.NetworkType == (byte)VCNetworkType.Intranet)
                    requiresParticipants = true;

                if (requiresParticipants && !dto.Participants.Any())
                    return Failure(400, "Participants must be selected for this booking.");

                // 6. Validate participants.
                var participantUserIds = dto.Participants
                    .Where(x => !string.IsNullOrWhiteSpace(x.ParticipantUserId))
                    .Select(x => x.ParticipantUserId!)
                    .Distinct()
                    .ToList();

                var activeParticipantIds = await _db.UserProfiles
                    .Where(x => x.IsActive &&
                                participantUserIds.Contains(x.UserId))
                    .Select(x => x.UserId)
                    .ToListAsync(cancellationToken);

                if (activeParticipantIds.Count != participantUserIds.Count)
                    return Failure(400, "One or more selected participants are invalid or inactive.");

                // 7. Start transaction and acquire hall-specific lock.
                await using var transaction =
                    await _db.Database.BeginTransactionAsync(
                        IsolationLevel.ReadCommitted,
                        cancellationToken);

                var lockResult = await AcquireHallLock(
                    dto.ConferenceHallId,
                    cancellationToken);

                if (lockResult < 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "Unable to acquire hall booking lock. Please retry.");
                }

                // 8. Check reservation ownership.
                ReservationOccurrence? ownedOccurrence = null;

                if (dto.ReservationOccurrenceId.HasValue)
                {
                    ownedOccurrence = await _db.ReservationOccurrences
                        .FirstOrDefaultAsync(
                            x => x.ReservationOccurrenceId ==
                                 dto.ReservationOccurrenceId.Value
                                 && x.ConferenceHallId == dto.ConferenceHallId
                                 && x.OccurrenceStatus ==
                                    (byte)ReservationOccurrenceStatus.Active,
                            cancellationToken);

                    if (ownedOccurrence == null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Failure(400, "Reservation occurrence is not active.");
                    }

                    var isOwner =
                        ownedOccurrence.ReservedForUserId == actorId ||
                        (ownedOccurrence.ReservedForDepartmentId ==
                         dto.BookingDepartmentId);

                    if (!isOwner)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Failure(403, "This reservation does not belong to your user or department.");
                    }

                    if (startUtc < ownedOccurrence.OccurrenceStartUtc ||
                        occupiedEndUtc > ownedOccurrence.OccurrenceEndUtc)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Failure(400, "Meeting and cooling time must fit within the reserved interval.");
                    }

                    if (ownedOccurrence.BookingId.HasValue)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Failure(409, "This reservation has already been used.");
                    }
                }

                // 9. Check existing bookings.
                var bookingConflict = await _db.Bookings.AnyAsync(
                    x => x.ConferenceHallId == dto.ConferenceHallId
                         && (x.ApprovalStatus ==
                                (byte)BookingApprovalStatus.Pending ||
                             x.ApprovalStatus ==
                                (byte)BookingApprovalStatus.Approved)
                         && startUtc <
                            x.MeetingEndUtc.AddMinutes(x.CoolingMinutes)
                         && occupiedEndUtc > x.MeetingStartUtc,
                    cancellationToken);

                if (bookingConflict)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "The selected hall slot conflicts with another booking.");
                }

                // 10. Check standalone active reservations.
                var reservationConflict = await _db.HallReservations.AnyAsync(
                    x => x.ConferenceHallId == dto.ConferenceHallId
                         && x.ReservationStatus ==
                            (byte)ReservationStatus.Active
                         && (!x.ExpiryAtUtc.HasValue ||
                             x.ExpiryAtUtc > DateTime.UtcNow)
                         && (!dto.ReservationOccurrenceId.HasValue ||
                             x.ReservationOccurrenceId !=
                             dto.ReservationOccurrenceId.Value)
                         && startUtc < x.ReservationEndUtc
                         && occupiedEndUtc > x.ReservationStartUtc,
                    cancellationToken);

                if (reservationConflict)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "The selected slot is reserved.");
                }

                // 11. Check active modification holds.
                var holdConflict = await _db.HallSlotHolds.AnyAsync(
                    x => x.ConferenceHallId == dto.ConferenceHallId
                         && x.HoldStatus == (byte)SlotHoldStatus.Active
                         && x.HoldExpiresAtUtc > DateTime.UtcNow
                         && startUtc < x.HoldEndUtc
                         && occupiedEndUtc > x.HoldStartUtc,
                    cancellationToken);

                if (holdConflict)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "The selected slot is temporarily held for a booking modification.");
                }

                // 12. Create booking.
                var now = DateTime.UtcNow;
                var automaticallyApproved = ownedOccurrence != null;

                var booking = new Booking
                {
                    BookingReferenceNo =
                        $"CNV-{now:yyyyMMdd}-{Guid.NewGuid():N}".Substring(0, 24),

                    ConferenceHallId = dto.ConferenceHallId,
                    BookingDepartmentId = dto.BookingDepartmentId,
                    RequestedByUserId = actorId,

                    ResponsiblePersonName = dto.ResponsiblePersonName.Trim(),
                    ResponsiblePersonDesignation = dto.ResponsiblePersonDesignation.Trim(),
                    ResponsiblePersonMobile = dto.ResponsiblePersonMobile.Trim(),

                    MeetingSubject = dto.MeetingSubject.Trim(),
                    ChairedBy = dto.ChairedBy.Trim(),

                    MeetingStartUtc = startUtc,
                    MeetingEndUtc = endUtc,
                    CoolingMinutes = coolingMinutes,

                    BookingType = (byte)BookingType.Normal,
                    ApprovalStatus = automaticallyApproved
                        ? (byte)BookingApprovalStatus.Approved
                        : (byte)BookingApprovalStatus.Pending,

                    MeetingStatus = (byte)MeetingStatus.NotStarted,
                    ReservationOccurrenceId = dto.ReservationOccurrenceId,

                    SubmittedAtUtc = now,
                    ApprovedByUserId = automaticallyApproved ? actorId : null,
                    ApprovedAtUtc = automaticallyApproved ? now : null,

                    CreatedAtUtc = now,
                    CreatedByUserId = actorId
                };

                _db.Bookings.Add(booking);
                await _db.SaveChangesAsync(cancellationToken);

                // 13. Save participants.
                foreach (var participant in dto.Participants)
                {
                    _db.BookingParticipants.Add(new BookingParticipant
                    {
                        BookingId = booking.BookingId,
                        ParticipantUserId = participant.ParticipantUserId,
                        ParticipantName = participant.ParticipantName.Trim(),
                        ParticipantDesignation = participant.ParticipantDesignation,
                        ParticipantDepartmentId = participant.ParticipantDepartmentId,
                        ParticipantEmail = participant.ParticipantEmail,
                        ParticipantType = participant.ParticipantType,
                        IsRequired = participant.IsRequired,
                        AttendanceStatus =
                            (byte)ParticipantAttendanceStatus.Unknown,
                        AddedByUserId = actorId,
                        AddedAtUtc = now,
                        IsActive = true
                    });
                }

                // 14. Save facility selections.
                foreach (var facility in dto.Facilities.Where(x => x.IsRequested))
                {
                    _db.BookingFacilityMaps.Add(new BookingFacilityMap
                    {
                        BookingId = booking.BookingId,
                        FacilityId = facility.FacilityId,
                        IsRequested = true,
                        Remarks = facility.Remarks
                    });
                }

                // 15. Save VC details without encryption.
                if (selectedPlatform != null && dto.VCCredentials != null)
                {
                    _db.BookingVCDetails.Add(new BookingVCDetails
                    {
                        BookingId = booking.BookingId,
                        VCPlatformId = selectedPlatform.VCPlatformId,

                        VCMeetingIdEncrypted = Encoding.UTF8.GetBytes(
                            dto.VCCredentials.VCMeetingId),

                        VCPasswordEncrypted = Encoding.UTF8.GetBytes(
                            dto.VCCredentials.VCPassword),

                        EncryptionKeyVersion = "PLAIN",
                        VCInstructions = dto.VCCredentials.VCInstructions,
                        CreatedAtUtc = now
                    });
                }

                // 16. Reservation occurrence becomes booked.
                if (ownedOccurrence != null)
                {
                    ownedOccurrence.OccurrenceStatus =
                        (byte)ReservationOccurrenceStatus.Booked;

                    ownedOccurrence.BookingId = booking.BookingId;
                }

                // 17. Status history.
                _db.BookingStatusHistory.Add(new BookingStatusHistory
                {
                    BookingId = booking.BookingId,
                    OldApprovalStatus = null,
                    NewApprovalStatus = booking.ApprovalStatus,
                    OldMeetingStatus = null,
                    NewMeetingStatus = booking.MeetingStatus,
                    ChangedByUserId = actorId,
                    RepresentedDepartmentId = dto.BookingDepartmentId,
                    Remarks = automaticallyApproved
                        ? "Automatically approved under designated reservation."
                        : "Booking request submitted.",
                    ChangedAtUtc = now
                });

                // 18. Audit entry. Never include VC credentials.
                _db.BookingAuditLogs.Add(new BookingAuditLog
                {
                    EntityType = "Booking",
                    EntityId = booking.BookingId,
                    ActionType = "BookingSubmitted",
                    PerformedByUserId = actorId,
                    RepresentedDepartmentId = dto.BookingDepartmentId,
                    NewValuesJson =
                        System.Text.Json.JsonSerializer.Serialize(new
                        {
                            booking.BookingReferenceNo,
                            booking.ConferenceHallId,
                            booking.BookingDepartmentId,
                            booking.MeetingStartUtc,
                            booking.MeetingEndUtc,
                            booking.ApprovalStatus
                        }),
                    Remarks = "Booking request created.",
                    CreatedAtUtc = now
                });

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                response.IsSuccess = true;
                response.StatusCode = 201;
                response.Message = automaticallyApproved
                    ? "Booking created and automatically approved under reservation."
                    : "Booking request submitted successfully.";

                response.Result = new
                {
                    booking.BookingId,
                    booking.BookingReferenceNo,
                    booking.ConferenceHallId,
                    booking.BookingDepartmentId,
                    booking.MeetingStartUtc,
                    booking.MeetingEndUtc,
                    booking.ApprovalStatus,
                    booking.MeetingStatus
                };

                return response;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return Failure(500, "An unexpected error occurred while submitting the booking.");
            }
        }

        public async Task<ResponseDto> CheckAvailability(
            BookingAvailabilityQueryDto dto,
            CancellationToken cancellationToken = default)
        {
            if (dto.FromUtc.Kind == DateTimeKind.Local ||
                dto.ToUtc.Kind == DateTimeKind.Local)
            {
                return Failure(400, "Dates must be supplied in UTC.");
            }

            var startUtc = DateTime.SpecifyKind(dto.FromUtc, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(dto.ToUtc, DateTimeKind.Utc);

            if (endUtc <= startUtc)
                return Failure(400, "End time must be later than start time.");

            var hallExists = await _db.ConferenceHalls.AnyAsync(
                x => x.ConferenceHallId == dto.ConferenceHallId && x.IsActive,
                cancellationToken);

            if (!hallExists)
                return Failure(404, "Conference hall not found or inactive.");

            var result = new BookingAvailabilityResultDto
            {
                ConferenceHallId = dto.ConferenceHallId,
                FromUtc = startUtc,
                ToUtc = endUtc
            };

            var bookingConflict = await _db.Bookings.AnyAsync(
                x => x.ConferenceHallId == dto.ConferenceHallId
                     && x.BookingId != dto.ExcludeBookingId
                     && (x.ApprovalStatus ==
                            (byte)BookingApprovalStatus.Pending ||
                         x.ApprovalStatus ==
                            (byte)BookingApprovalStatus.Approved)
                     && startUtc <
                        x.MeetingEndUtc.AddMinutes(x.CoolingMinutes)
                     && endUtc.AddMinutes(DefaultCoolingMinutes) >
                        x.MeetingStartUtc,
                cancellationToken);

            if (bookingConflict)
                result.Conflicts.Add("Another booking occupies this time slot.");

            var reservationConflict = await _db.HallReservations.AnyAsync(
                x => x.ConferenceHallId == dto.ConferenceHallId
                     && x.ReservationStatus ==
                        (byte)ReservationStatus.Active
                     && (!x.ExpiryAtUtc.HasValue ||
                         x.ExpiryAtUtc > DateTime.UtcNow)
                     && startUtc < x.ReservationEndUtc
                     && endUtc.AddMinutes(DefaultCoolingMinutes) >
                        x.ReservationStartUtc,
                cancellationToken);

            if (reservationConflict)
                result.Conflicts.Add("This time slot is reserved.");

            var holdConflict = await _db.HallSlotHolds.AnyAsync(
                x => x.ConferenceHallId == dto.ConferenceHallId
                     && x.HoldStatus == (byte)SlotHoldStatus.Active
                     && x.HoldExpiresAtUtc > DateTime.UtcNow
                     && startUtc < x.HoldEndUtc
                     && endUtc.AddMinutes(DefaultCoolingMinutes) >
                        x.HoldStartUtc,
                cancellationToken);

            if (holdConflict)
                result.Conflicts.Add("This time slot is temporarily held.");

            result.IsAvailable = result.Conflicts.Count == 0;

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Availability checked.",
                Result = result
            };
        }

        private async Task<int> AcquireHallLock(
            int hallId,
            CancellationToken cancellationToken)
        {
            var connection = _db.Database.GetDbConnection();

            await using var command = connection.CreateCommand();

            command.Transaction = _db.Database
                .CurrentTransaction!
                .GetDbTransaction();

            command.CommandText = @"
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock
                    @Resource = @resource,
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = 10000;
                SELECT @result;";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@resource";
            parameter.Value = $"CONVENE_HALL_BOOKING_{hallId}";
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);

            return Convert.ToInt32(result);
        }

        private static ResponseDto Failure(int statusCode, string message)
        {
            return new ResponseDto
            {
                IsSuccess = false,
                StatusCode = statusCode,
                Message = message
            };
        }
    }
}