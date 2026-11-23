using ConferenceBooking.Services.BookingAPI.Data;
using ConferenceBooking.Services.BookingAPI.Enums;
using ConferenceBooking.Services.BookingAPI.Models;
using ConferenceBooking.Services.BookingAPI.Models.Dto;
using ConferenceBooking.Services.BookingAPI.Service.IService;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;

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

                // Persist booking and all associated records first.
                await _db.SaveChangesAsync(cancellationToken);

                // Queue submission notifications only for bookings requiring approval.
                // Automatically approved reservation bookings will be handled
                // by the BookingApproved notification integration.
                if (!automaticallyApproved)
                {
                    await QueueBookingSubmittedNotificationsAsync(
                        booking,
                        profile,
                        transaction,
                        cancellationToken);
                }
                else
                {
                    // Reservation-based bookings are automatically approved.
                    await QueueVCMeetingInvitationsAsync(
                        booking,
                        cancellationToken);
                }

                // Persist outbox records in the same transaction.
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


        public async Task<ResponseDto> GetPendingBookings(
            CancellationToken cancellationToken = default)
        {
            if (!_currentUser.IsAuthenticated)
                return Failure(401, "Authentication is required.");

            if (!_currentUser.IsInRole("Admin") &&
                !_currentUser.IsInRole("SUBADMIN"))
                return Failure(403, "You are not authorized to view pending approvals.");

            var query = from booking in _db.Bookings.AsNoTracking()
                        join hall in _db.ConferenceHalls.AsNoTracking()
                            on booking.ConferenceHallId equals hall.ConferenceHallId
                        where booking.ApprovalStatus ==
                            (byte)BookingApprovalStatus.Pending
                        select new
                        {
                            booking.BookingId,
                            booking.BookingReferenceNo,
                            booking.ConferenceHallId,
                            hall.HallName,
                            booking.BookingDepartmentId,
                            booking.RequestedByUserId,
                            booking.ResponsiblePersonName,
                            booking.ResponsiblePersonDesignation,
                            booking.ResponsiblePersonMobile,
                            booking.MeetingSubject,
                            booking.ChairedBy,
                            booking.MeetingStartUtc,
                            booking.MeetingEndUtc,
                            booking.SubmittedAtUtc
                        };

            // Sub Admin sees only bookings for assigned halls.
            if (!_currentUser.IsInRole("Admin"))
            {
                var userId = _currentUser.UserId;

                var assignedHallIds = _db.HallSubAdminMaps
                    .Where(x => x.UserId == userId && x.IsActive)
                    .Select(x => x.ConferenceHallId);

                query = query.Where(x =>
                    assignedHallIds.Contains(x.ConferenceHallId));
            }

            var result = await query
                .OrderBy(x => x.MeetingStartUtc)
                .ToListAsync(cancellationToken);

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Pending bookings retrieved successfully.",
                Result = result
            };
        }

        public async Task<ResponseDto> ApproveBooking(
            BookingDecisionDto dto,
            CancellationToken cancellationToken = default)
        {
            if (!_currentUser.IsAuthenticated)
                return Failure(401, "Authentication is required.");

            var actorId = _currentUser.UserId;

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

            try
            {
                var booking = await _db.Bookings.FirstOrDefaultAsync(
                    x => x.BookingId == dto.BookingId,
                    cancellationToken);

                if (booking == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(404, "Booking not found.");
                }

                var authorized = await CanManageHall(
                    booking.ConferenceHallId,
                    cancellationToken);

                if (!authorized)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(403, "You are not authorized to approve bookings for this hall.");
                }

                if (booking.ApprovalStatus !=
                    (byte)BookingApprovalStatus.Pending)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "Only pending bookings can be approved.");
                }

                var lockResult = await AcquireHallLock(
                    booking.ConferenceHallId,
                    cancellationToken);

                if (lockResult < 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "Unable to acquire hall booking lock. Please retry.");
                }

                // Reload after acquiring the lock to avoid acting on stale state.
                await _db.Entry(booking).ReloadAsync(cancellationToken);

                if (booking.ApprovalStatus !=
                    (byte)BookingApprovalStatus.Pending)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "Booking status has changed. Refresh and retry.");
                }

                var occupiedEndUtc = booking.MeetingEndUtc.AddMinutes(
                    booking.CoolingMinutes);

                // Recheck other pending and approved bookings.
                var bookingConflict = await _db.Bookings.AnyAsync(
                    x => x.BookingId != booking.BookingId
                         && x.ConferenceHallId == booking.ConferenceHallId
                         && (x.ApprovalStatus ==
                                (byte)BookingApprovalStatus.Pending ||
                             x.ApprovalStatus ==
                                (byte)BookingApprovalStatus.Approved)
                         && booking.MeetingStartUtc <
                            x.MeetingEndUtc.AddMinutes(x.CoolingMinutes)
                         && occupiedEndUtc > x.MeetingStartUtc,
                    cancellationToken);

                if (bookingConflict)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "Another booking now conflicts with this slot.");
                }

                var reservationConflict = await _db.HallReservations.AnyAsync(
                    x => x.ConferenceHallId == booking.ConferenceHallId
                         && x.ReservationStatus == (byte)ReservationStatus.Active
                         && (!x.ExpiryAtUtc.HasValue ||
                             x.ExpiryAtUtc > DateTime.UtcNow)
                         && (!booking.ReservationOccurrenceId.HasValue ||
                             x.ReservationOccurrenceId !=
                             booking.ReservationOccurrenceId.Value)
                         && booking.MeetingStartUtc < x.ReservationEndUtc
                         && occupiedEndUtc > x.ReservationStartUtc,
                    cancellationToken);

                if (reservationConflict)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "The booking slot is now reserved.");
                }

                var holdConflict = await _db.HallSlotHolds.AnyAsync(
                    x => x.ConferenceHallId == booking.ConferenceHallId
                         && x.HoldStatus == (byte)SlotHoldStatus.Active
                         && x.HoldExpiresAtUtc > DateTime.UtcNow
                         && booking.MeetingStartUtc < x.HoldEndUtc
                         && occupiedEndUtc > x.HoldStartUtc,
                    cancellationToken);

                if (holdConflict)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "The booking slot is currently held for modification.");
                }

                var oldStatus = booking.ApprovalStatus;
                var now = DateTime.UtcNow;
                var departmentId =
                    await _currentUser.GetDepartmentIdAsync(cancellationToken);

                booking.ApprovalStatus = (byte)BookingApprovalStatus.Approved;
                booking.ApprovedByUserId = actorId;
                booking.ApprovedAtUtc = now;
                booking.UpdatedAtUtc = now;
                booking.UpdatedByUserId = actorId;

                _db.BookingStatusHistory.Add(new BookingStatusHistory
                {
                    BookingId = booking.BookingId,
                    OldApprovalStatus = oldStatus,
                    NewApprovalStatus = booking.ApprovalStatus,
                    OldMeetingStatus = booking.MeetingStatus,
                    NewMeetingStatus = booking.MeetingStatus,
                    ChangedByUserId = actorId,
                    RepresentedDepartmentId = departmentId,
                    Remarks = dto.Remarks ?? "Booking approved.",
                    ChangedAtUtc = now
                });

                _db.BookingAuditLogs.Add(new BookingAuditLog
                {
                    EntityType = "Booking",
                    EntityId = booking.BookingId,
                    ActionType = "BookingApproved",
                    PerformedByUserId = actorId,
                    RepresentedDepartmentId = departmentId,
                    OldValuesJson = System.Text.Json.JsonSerializer.Serialize(
                        new { ApprovalStatus = oldStatus }),
                    NewValuesJson = System.Text.Json.JsonSerializer.Serialize(
                        new { ApprovalStatus = booking.ApprovalStatus }),
                    Remarks = dto.Remarks ?? "Booking approved.",
                    CreatedAtUtc = now
                });

                // Persist the approval and status history first.
                await _db.SaveChangesAsync(cancellationToken);

                // Queue approval notification to the original requester.
                await QueueBookingApprovedNotificationAsync(
                    booking,
                    actorId,
                    dto.Remarks,
                    cancellationToken);

                // Send separate VC invitations to selected participants.
                await QueueVCMeetingInvitationsAsync(
                    booking,
                    cancellationToken);

                // Persist outbox notification in the same transaction.
                await _db.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return new ResponseDto
                {
                    IsSuccess = true,
                    StatusCode = 200,
                    Message = "Booking approved successfully.",
                    Result = new
                    {
                        booking.BookingId,
                        booking.BookingReferenceNo,
                        booking.ApprovalStatus,
                        booking.ApprovedByUserId,
                        booking.ApprovedAtUtc
                    }
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Failure(500, "An unexpected error occurred while approving the booking.");
            }
        }

        public async Task<ResponseDto> RejectBooking(
            RejectBookingDto dto,
            CancellationToken cancellationToken = default)
        {
            if (!_currentUser.IsAuthenticated)
                return Failure(401, "Authentication is required.");

            if (string.IsNullOrWhiteSpace(dto.RejectionReason))
                return Failure(400, "Rejection reason is compulsory.");

            var actorId = _currentUser.UserId;

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

            try
            {
                var booking = await _db.Bookings.FirstOrDefaultAsync(
                    x => x.BookingId == dto.BookingId,
                    cancellationToken);

                if (booking == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(404, "Booking not found.");
                }

                if (!await CanManageHall(
                    booking.ConferenceHallId, cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(403, "You are not authorized to reject bookings for this hall.");
                }

                var lockResult = await AcquireHallLock(
                    booking.ConferenceHallId,
                    cancellationToken);

                if (lockResult < 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "Unable to acquire hall booking lock. Please retry.");
                }

                await _db.Entry(booking).ReloadAsync(cancellationToken);

                if (booking.ApprovalStatus !=
                    (byte)BookingApprovalStatus.Pending)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "Only pending bookings can be rejected.");
                }

                var oldStatus = booking.ApprovalStatus;
                var now = DateTime.UtcNow;
                var departmentId =
                    await _currentUser.GetDepartmentIdAsync(cancellationToken);

                booking.ApprovalStatus = (byte)BookingApprovalStatus.Rejected;
                booking.RejectionReason = dto.RejectionReason.Trim();
                booking.UpdatedAtUtc = now;
                booking.UpdatedByUserId = actorId;

                _db.BookingStatusHistory.Add(new BookingStatusHistory
                {
                    BookingId = booking.BookingId,
                    OldApprovalStatus = oldStatus,
                    NewApprovalStatus = booking.ApprovalStatus,
                    OldMeetingStatus = booking.MeetingStatus,
                    NewMeetingStatus = booking.MeetingStatus,
                    ChangedByUserId = actorId,
                    RepresentedDepartmentId = departmentId,
                    Remarks = booking.RejectionReason,
                    ChangedAtUtc = now
                });

                _db.BookingAuditLogs.Add(new BookingAuditLog
                {
                    EntityType = "Booking",
                    EntityId = booking.BookingId,
                    ActionType = "BookingRejected",
                    PerformedByUserId = actorId,
                    RepresentedDepartmentId = departmentId,
                    OldValuesJson = System.Text.Json.JsonSerializer.Serialize(
                        new { ApprovalStatus = oldStatus }),
                    NewValuesJson = System.Text.Json.JsonSerializer.Serialize(
                        new
                        {
                            ApprovalStatus = booking.ApprovalStatus,
                            booking.RejectionReason
                        }),
                    Remarks = dto.Remarks ?? booking.RejectionReason,
                    CreatedAtUtc = now
                });

                // Persist rejection and status history first.
                await _db.SaveChangesAsync(cancellationToken);

                // Notify only the original requester about the rejection.
                // No VC invitation or participant notification is sent.
                await QueueBookingRejectedNotificationAsync(
                    booking,
                    actorId,
                    booking.RejectionReason,
                    cancellationToken);

                // Persist the rejection notification in the same transaction.
                await _db.SaveChangesAsync(cancellationToken);

                return new ResponseDto
                {
                    IsSuccess = true,
                    StatusCode = 200,
                    Message = "Booking rejected successfully.",
                    Result = new
                    {
                        booking.BookingId,
                        booking.BookingReferenceNo,
                        booking.ApprovalStatus,
                        booking.RejectionReason
                    }
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Failure(500, "An unexpected error occurred while rejecting the booking.");
            }
        }

        public async Task<ResponseDto> CancelBooking(
            CancelBookingDto dto,
            CancellationToken cancellationToken = default)
        {
            if (!_currentUser.IsAuthenticated)
                return Failure(401, "Authentication is required.");

            if (string.IsNullOrWhiteSpace(dto.CancellationReason))
                return Failure(400, "Cancellation reason is compulsory.");

            var actorId = _currentUser.UserId;

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

            try
            {
                var booking = await _db.Bookings.FirstOrDefaultAsync(
                    x => x.BookingId == dto.BookingId,
                    cancellationToken);

                if (booking == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(404, "Booking not found.");
                }

                var isAdmin = _currentUser.IsInRole("Admin");
                var isRequester = booking.RequestedByUserId == actorId;

                var isSubAdmin = await IsAssignedSubAdmin(
                    booking.ConferenceHallId, cancellationToken);

                if (!isAdmin && !isRequester && !isSubAdmin)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(403, "You are not authorized to cancel this booking.");
                }

                var lockResult = await AcquireHallLock(
                    booking.ConferenceHallId,
                    cancellationToken);

                if (lockResult < 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "Unable to acquire hall booking lock. Please retry.");
                }

                await _db.Entry(booking).ReloadAsync(cancellationToken);

                if (booking.ApprovalStatus !=
                        (byte)BookingApprovalStatus.Pending &&
                    booking.ApprovalStatus !=
                        (byte)BookingApprovalStatus.Approved)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "Only pending or approved bookings can be cancelled.");
                }

                var oldStatus = booking.ApprovalStatus;
                var now = DateTime.UtcNow;
                var departmentId =
                    await _currentUser.GetDepartmentIdAsync(cancellationToken);

                booking.ApprovalStatus = (byte)BookingApprovalStatus.Cancelled;
                booking.CancelledByUserId = actorId;
                booking.CancelledAtUtc = now;
                booking.CancellationReason = dto.CancellationReason.Trim();
                booking.UpdatedAtUtc = now;
                booking.UpdatedByUserId = actorId;

                _db.BookingStatusHistory.Add(new BookingStatusHistory
                {
                    BookingId = booking.BookingId,
                    OldApprovalStatus = oldStatus,
                    NewApprovalStatus = booking.ApprovalStatus,
                    OldMeetingStatus = booking.MeetingStatus,
                    NewMeetingStatus = booking.MeetingStatus,
                    ChangedByUserId = actorId,
                    RepresentedDepartmentId = departmentId,
                    Remarks = booking.CancellationReason,
                    ChangedAtUtc = now
                });

                _db.BookingAuditLogs.Add(new BookingAuditLog
                {
                    EntityType = "Booking",
                    EntityId = booking.BookingId,
                    ActionType = "BookingCancelled",
                    PerformedByUserId = actorId,
                    RepresentedDepartmentId = departmentId,
                    OldValuesJson = System.Text.Json.JsonSerializer.Serialize(
                        new { ApprovalStatus = oldStatus }),
                    NewValuesJson = System.Text.Json.JsonSerializer.Serialize(
                        new
                        {
                            ApprovalStatus = booking.ApprovalStatus,
                            booking.CancellationReason
                        }),
                    Remarks = booking.CancellationReason,
                    CreatedAtUtc = now
                });

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new ResponseDto
                {
                    IsSuccess = true,
                    StatusCode = 200,
                    Message = "Booking cancelled successfully.",
                    Result = new
                    {
                        booking.BookingId,
                        booking.BookingReferenceNo,
                        booking.ApprovalStatus,
                        booking.CancellationReason
                    }
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Failure(500, "An unexpected error occurred while cancelling the booking.");
            }
        }

        private async Task<bool> CanManageHall(
            int hallId,
            CancellationToken cancellationToken)
        {
            if (_currentUser.IsInRole("Admin"))
                return true;

            return await IsAssignedSubAdmin(hallId, cancellationToken);
        }


        private async Task QueueBookingSubmittedNotificationsAsync(
            Booking booking,
            UserProfile requester,
            IDbContextTransaction transaction,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            var recipients = new List<(string Name, string Email)>();

            // 1. Resolve active Admin users through ASP.NET Core Identity tables.
            // Only active profiles with valid email addresses are eligible.
            var connection = _db.Database.GetDbConnection();

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();

                command.CommandText = @"
            SELECT DISTINCT
                p.DisplayName,
                p.Username,
                p.EmailAddress
            FROM dbo.AspNetUserRoles ur
            INNER JOIN dbo.AspNetRoles r
                ON r.Id = ur.RoleId
            INNER JOIN dbo.Tbl_UserProfile p
                ON p.UserId = ur.UserId
            WHERE UPPER(r.Name) = N'ADMIN'
              AND p.IsActive = 1
              AND NULLIF(LTRIM(RTRIM(p.EmailAddress)), '') IS NOT NULL;";

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var displayName = reader.IsDBNull(0)
                        ? string.Empty
                        : reader.GetString(0);

                    var username = reader.IsDBNull(1)
                        ? string.Empty
                        : reader.GetString(1);

                    var email = reader.IsDBNull(2)
                        ? string.Empty
                        : reader.GetString(2).Trim();

                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        recipients.Add((
                            string.IsNullOrWhiteSpace(displayName)
                                ? username
                                : displayName,
                            email));
                    }
                }
            }

            // 2. Resolve only the active SubAdmins assigned to this hall.
            var subAdminRecipients = await (
                from map in _db.HallSubAdminMaps
                join user in _db.UserProfiles
                    on map.UserId equals user.UserId
                where map.ConferenceHallId == booking.ConferenceHallId
                      && map.IsActive
                      && map.EffectiveFromUtc <= now
                      && (!map.EffectiveToUtc.HasValue ||
                          map.EffectiveToUtc >= now)
                      && user.IsActive
                      && user.EmailAddress != ""
                select new
                {
                    user.DisplayName,
                    user.Username,
                    user.EmailAddress
                }
            )
            .AsNoTracking()
            .ToListAsync(cancellationToken);

            foreach (var user in subAdminRecipients)
            {
                if (!string.IsNullOrWhiteSpace(user.EmailAddress))
                {
                    recipients.Add((
                        string.IsNullOrWhiteSpace(user.DisplayName)
                            ? user.Username
                            : user.DisplayName,
                        user.EmailAddress.Trim()));
                }
            }

            // 3. Deduplicate recipients by email address.
            var uniqueRecipients = recipients
                .Where(x => !string.IsNullOrWhiteSpace(x.Email))
                .GroupBy(
                    x => x.Email,
                    StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToList();

            if (uniqueRecipients.Count == 0)
            {
                throw new InvalidOperationException(
                    "No active Admin or assigned SubAdmin email recipients " +
                    "were found for this booking.");
            }

            // 4. Resolve hall name.
            var hallName = await _db.ConferenceHalls
                .AsNoTracking()
                .Where(x => x.ConferenceHallId == booking.ConferenceHallId)
                .Select(x => x.HallName)
                .FirstAsync(cancellationToken);

            // 5. Format meeting timings in Indian Standard Time.
            var indiaTimeZone =
                TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

            var meetingStart = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(
                    booking.MeetingStartUtc,
                    DateTimeKind.Utc),
                indiaTimeZone);

            var meetingEnd = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(
                    booking.MeetingEndUtc,
                    DateTimeKind.Utc),
                indiaTimeZone);

            var startText = meetingStart.ToString(
                "dd MMM yyyy, hh:mm tt",
                CultureInfo.InvariantCulture) + " IST";

            var endText = meetingEnd.ToString(
                "dd MMM yyyy, hh:mm tt",
                CultureInfo.InvariantCulture) + " IST";

            // 6. Create one outbox notification per recipient.
            foreach (var recipient in uniqueRecipients)
            {
                var payload = new
                {
                    RecipientName = recipient.Name,
                    RequesterName = requester.DisplayName,
                    BookingReference = booking.BookingReferenceNo,
                    MeetingSubject = booking.MeetingSubject,
                    HallName = hallName,
                    MeetingStart = startText,
                    MeetingEnd = endText,
                    ActionBy = requester.DisplayName,
                    Remarks = "A new booking request has been submitted " +
                              "and is awaiting approval."
                };

                _db.EmailNotificationOutbox.Add(
                    new EmailNotificationOutbox
                    {
                        EventType = "BookingSubmitted",
                        ToEmail = recipient.Email,
                        CcEmail = null,
                        BccEmail = null,
                        Subject = null,

                        PayloadJson = JsonSerializer.Serialize(payload),

                        Status = EmailNotificationStatus.Pending,
                        AttemptCount = 0,
                        MaxAttempts = 5,
                        NextAttemptAtUtc = now,

                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    });
            }
        }


        private async Task QueueBookingApprovedNotificationAsync(
            Booking booking,
            string approvedByUserId,
            string? remarks,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            // Resolve the original requester.
            var requester = await _db.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.UserId == booking.RequestedByUserId && x.IsActive,
                    cancellationToken);

            // Approval must not fail merely because the requester has no email.
            if (requester == null ||
                string.IsNullOrWhiteSpace(requester.EmailAddress))
            {
                return;
            }

            // Resolve the approving officer's display name.
            var approver = await _db.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.UserId == approvedByUserId && x.IsActive,
                    cancellationToken);

            var approverName = approver == null
                ? "Conference Administration"
                : !string.IsNullOrWhiteSpace(approver.DisplayName)
                    ? approver.DisplayName
                    : approver.Username;

            var requesterName = !string.IsNullOrWhiteSpace(requester.DisplayName)
                ? requester.DisplayName
                : requester.Username;

            // Resolve conference hall.
            var hallName = await _db.ConferenceHalls
                .AsNoTracking()
                .Where(x => x.ConferenceHallId == booking.ConferenceHallId)
                .Select(x => x.HallName)
                .FirstOrDefaultAsync(cancellationToken)
                ?? "Conference Hall";

            // Format meeting timings in Indian Standard Time.
            var indiaTimeZone =
                TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

            var meetingStart = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(
                    booking.MeetingStartUtc,
                    DateTimeKind.Utc),
                indiaTimeZone);

            var meetingEnd = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(
                    booking.MeetingEndUtc,
                    DateTimeKind.Utc),
                indiaTimeZone);

            var startText = meetingStart.ToString(
                "dd MMM yyyy, hh:mm tt",
                CultureInfo.InvariantCulture) + " IST";

            var endText = meetingEnd.ToString(
                "dd MMM yyyy, hh:mm tt",
                CultureInfo.InvariantCulture) + " IST";

            var payload = new
            {
                RecipientName = requesterName,
                RequesterName = requesterName,
                BookingReference = booking.BookingReferenceNo,
                MeetingSubject = booking.MeetingSubject,
                HallName = hallName,
                MeetingStart = startText,
                MeetingEnd = endText,
                ActionBy = approverName,
                Remarks = string.IsNullOrWhiteSpace(remarks)
                    ? "Your booking request has been approved."
                    : remarks.Trim()
            };

            _db.EmailNotificationOutbox.Add(
                new EmailNotificationOutbox
                {
                    EventType = "BookingApproved",
                    ToEmail = requester.EmailAddress.Trim(),
                    CcEmail = null,
                    BccEmail = null,
                    Subject = null,

                    PayloadJson = JsonSerializer.Serialize(payload),

                    Status = EmailNotificationStatus.Pending,
                    AttemptCount = 0,
                    MaxAttempts = 5,
                    NextAttemptAtUtc = now,

                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
        }


        private async Task QueueVCMeetingInvitationsAsync(
            Booking booking,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            // A VC invitation is required only when VC details exist.
            var hasVCDetails = await _db.BookingVCDetails
                .AnyAsync(
                    x => x.BookingId == booking.BookingId,
                    cancellationToken);

            if (!hasVCDetails)
                return;

            // Get active, selected participants having an email address.
            var participants = await _db.BookingParticipants
                .AsNoTracking()
                .Where(x =>
                    x.BookingId == booking.BookingId &&
                    x.IsActive &&
                    x.ParticipantEmail != null &&
                    x.ParticipantEmail != "")
                .Select(x => new
                {
                    x.BookingParticipantId,
                    x.ParticipantEmail
                })
                .ToListAsync(cancellationToken);

            // One invitation per unique email address.
            var uniqueParticipants = participants
                .Where(x => !string.IsNullOrWhiteSpace(x.ParticipantEmail))
                .GroupBy(
                    x => x.ParticipantEmail!.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToList();

            foreach (var participant in uniqueParticipants)
            {
                var payload = new
                {
                    BookingId = booking.BookingId,
                    BookingParticipantId = participant.BookingParticipantId
                };

                _db.EmailNotificationOutbox.Add(
                    new EmailNotificationOutbox
                    {
                        EventType = "VCMeetingInvitation",
                        ToEmail = participant.ParticipantEmail!.Trim(),
                        CcEmail = null,
                        BccEmail = null,
                        Subject = null,

                        // No VC credentials are stored in the outbox payload.
                        PayloadJson = JsonSerializer.Serialize(payload),

                        Status = EmailNotificationStatus.Pending,
                        AttemptCount = 0,
                        MaxAttempts = 5,
                        NextAttemptAtUtc = now,

                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    });
            }
        }

        private async Task QueueBookingRejectedNotificationAsync(
            Booking booking,
            string rejectedByUserId,
            string rejectionReason,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            // Resolve the original requester.
            var requester = await _db.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.UserId == booking.RequestedByUserId && x.IsActive,
                    cancellationToken);

            // Rejection must not fail merely because the requester has no email.
            if (requester == null ||
                string.IsNullOrWhiteSpace(requester.EmailAddress))
            {
                return;
            }

            // Resolve the rejecting officer's display name.
            var rejectingOfficer = await _db.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.UserId == rejectedByUserId && x.IsActive,
                    cancellationToken);

            var rejectingOfficerName = rejectingOfficer == null
                ? "Conference Administration"
                : !string.IsNullOrWhiteSpace(rejectingOfficer.DisplayName)
                    ? rejectingOfficer.DisplayName
                    : rejectingOfficer.Username;

            var requesterName = !string.IsNullOrWhiteSpace(requester.DisplayName)
                ? requester.DisplayName
                : requester.Username;

            // Resolve conference hall.
            var hallName = await _db.ConferenceHalls
                .AsNoTracking()
                .Where(x => x.ConferenceHallId == booking.ConferenceHallId)
                .Select(x => x.HallName)
                .FirstOrDefaultAsync(cancellationToken)
                ?? "Conference Hall";

            // Format meeting timings in Indian Standard Time.
            var indiaTimeZone =
                TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

            var meetingStart = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(
                    booking.MeetingStartUtc,
                    DateTimeKind.Utc),
                indiaTimeZone);

            var meetingEnd = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(
                    booking.MeetingEndUtc,
                    DateTimeKind.Utc),
                indiaTimeZone);

            var startText = meetingStart.ToString(
                "dd MMM yyyy, hh:mm tt",
                CultureInfo.InvariantCulture) + " IST";

            var endText = meetingEnd.ToString(
                "dd MMM yyyy, hh:mm tt",
                CultureInfo.InvariantCulture) + " IST";

            var payload = new
            {
                RecipientName = requesterName,
                RequesterName = requesterName,
                BookingReference = booking.BookingReferenceNo,
                MeetingSubject = booking.MeetingSubject,
                HallName = hallName,
                MeetingStart = startText,
                MeetingEnd = endText,
                ActionBy = rejectingOfficerName,
                Remarks = string.IsNullOrWhiteSpace(rejectionReason)
                    ? "Your booking request has been rejected."
                    : rejectionReason.Trim()
            };

            _db.EmailNotificationOutbox.Add(
                new EmailNotificationOutbox
                {
                    EventType = "BookingRejected",
                    ToEmail = requester.EmailAddress.Trim(),
                    CcEmail = null,
                    BccEmail = null,
                    Subject = null,

                    PayloadJson = JsonSerializer.Serialize(payload),

                    Status = EmailNotificationStatus.Pending,
                    AttemptCount = 0,
                    MaxAttempts = 5,
                    NextAttemptAtUtc = now,

                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
        }

        private async Task<bool> IsAssignedSubAdmin(
            int hallId,
            CancellationToken cancellationToken)
        {
            if (!_currentUser.IsInRole("SUBADMIN"))
                return false;

            var userId = _currentUser.UserId;
            var now = DateTime.UtcNow;

            return await _db.HallSubAdminMaps.AnyAsync(
                x => x.ConferenceHallId == hallId
                     && x.UserId == userId
                     && x.IsActive
                     && x.EffectiveFromUtc <= now
                     && (!x.EffectiveToUtc.HasValue ||
                         x.EffectiveToUtc >= now),
                cancellationToken);
        }

    }
}