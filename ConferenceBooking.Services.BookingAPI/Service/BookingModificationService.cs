
using System.Data;
using System.Text;
using System.Text.Json;
using ConferenceBooking.Services.BookingAPI.Data;
using ConferenceBooking.Services.BookingAPI.Enums;
using ConferenceBooking.Services.BookingAPI.Models;
using ConferenceBooking.Services.BookingAPI.Models.Dto;
using ConferenceBooking.Services.BookingAPI.Service.IService;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ConferenceBooking.Services.BookingAPI.Service
{
    public class BookingModificationService : IBookingModificationService
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUser;
        private readonly IConfiguration _configuration;

        private const short DefaultCoolingMinutes = 15;

        public BookingModificationService(
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            IConfiguration configuration)
        {
            _db = db;
            _currentUser = currentUser;
            _configuration = configuration;
        }

        public async Task<ResponseDto> Submit(
            BookingModificationDto dto,
            CancellationToken cancellationToken = default)
        {
            if (!_currentUser.IsAuthenticated ||
                string.IsNullOrWhiteSpace(_currentUser.UserId))
                return Failure(401, "Authentication is required.");

            var actorId = _currentUser.UserId;
            var now = DateTime.UtcNow;

            if (dto.ProposedStartUtc.Kind == DateTimeKind.Local ||
                dto.ProposedEndUtc.Kind == DateTimeKind.Local)
                return Failure(400, "Proposed dates must be supplied in UTC.");

            var proposedStart = DateTime.SpecifyKind(
                dto.ProposedStartUtc, DateTimeKind.Utc);

            var proposedEnd = DateTime.SpecifyKind(
                dto.ProposedEndUtc, DateTimeKind.Utc);

            if (proposedStart <= now || proposedEnd <= proposedStart)
                return Failure(400, "The proposed meeting period is invalid.");

            var booking = await _db.Bookings
                .FirstOrDefaultAsync(
                    x => x.BookingId == dto.BookingId,
                    cancellationToken);

            if (booking == null)
                return Failure(404, "Booking not found.");

            if (booking.ApprovalStatus !=
                (byte)BookingApprovalStatus.Approved)
                return Failure(400, "Only approved bookings can be modified.");

            if (booking.MeetingStatus != (byte)MeetingStatus.NotStarted ||
                booking.MeetingStartUtc <= now)
                return Failure(400, "A meeting that has started or ended cannot be modified.");

            var isRequester = booking.RequestedByUserId == actorId;
            var isAdmin = _currentUser.IsInRole("Admin");
            var isSubAdmin = await IsAssignedSubAdmin(
                booking.ConferenceHallId, cancellationToken);

            if (!isRequester && !isAdmin && !isSubAdmin)
                return Failure(403, "You are not authorized to modify this booking.");

            var profile = await _db.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.UserId == actorId && x.IsActive,
                    cancellationToken);

            if (profile?.DepartmentId == null)
                return Failure(403, "An active user profile and department are required.");

            var proposedHall = await _db.ConferenceHalls
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.ConferenceHallId == dto.ProposedHallId &&
                         x.IsActive,
                    cancellationToken);

            if (proposedHall == null)
                return Failure(400, "The proposed hall is invalid or inactive.");

            if (string.IsNullOrWhiteSpace(dto.ChangeReason))
                return Failure(400, "Change reason is required.");

            var holdHours = _configuration.GetValue<int?>(
                "Booking:ModificationHoldHours") ?? 24;

            if (holdHours < 1 || holdHours > 168)
                return Failure(500, "Modification hold duration configuration must be between 1 and 168 hours.");

            var holdExpiry = now.AddHours(holdHours);
            var occupiedEnd = proposedEnd.AddMinutes(
                booking.CoolingMinutes > 0
                    ? booking.CoolingMinutes
                    : DefaultCoolingMinutes);

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

            try
            {
                // Lock both halls in ascending order to avoid deadlocks.
                var hallIds = new[]
                {
                    booking.ConferenceHallId,
                    dto.ProposedHallId
                }.Distinct().OrderBy(x => x).ToArray();

                foreach (var hallId in hallIds)
                {
                    var lockResult = await AcquireHallLock(
                        hallId, cancellationToken);

                    if (lockResult < 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Failure(409, "Unable to acquire hall booking lock. Please retry.");
                    }
                }

                // Reload booking after acquiring locks.
                await _db.Entry(booking).ReloadAsync(cancellationToken);

                if (booking.ApprovalStatus !=
                    (byte)BookingApprovalStatus.Approved ||
                    booking.MeetingStatus != (byte)MeetingStatus.NotStarted ||
                    booking.MeetingStartUtc <= DateTime.UtcNow)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "The booking is no longer eligible for modification.");
                }

                // Do not allow multiple pending modification requests.
                var existingRequest = await _db.BookingModificationRequests
                    .AnyAsync(
                        x => x.BookingId == booking.BookingId &&
                             x.ModificationStatus ==
                                 (byte)ModificationStatus.Pending &&
                             x.HoldExpiresAtUtc > DateTime.UtcNow,
                        cancellationToken);

                if (existingRequest)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "A pending modification request already exists for this booking.");
                }

                // A booking inside a reservation must remain inside that
                // reservation occurrence, including cooling time.
                if (booking.ReservationOccurrenceId.HasValue)
                {
                    var occurrence = await _db.ReservationOccurrences
                        .FirstOrDefaultAsync(
                            x => x.ReservationOccurrenceId ==
                                 booking.ReservationOccurrenceId.Value &&
                                 x.OccurrenceStatus ==
                                 (byte)ReservationOccurrenceStatus.Booked,
                            cancellationToken);

                    if (occurrence == null ||
                        dto.ProposedHallId != occurrence.ConferenceHallId ||
                        proposedStart < occurrence.OccurrenceStartUtc ||
                        occupiedEnd > occurrence.OccurrenceEndUtc)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Failure(400, "A reservation-based booking must remain within its reserved hall and interval.");
                    }
                }

                // Check pending and approved bookings, excluding this booking.
                var bookingConflict = await _db.Bookings.AnyAsync(
                    x => x.BookingId != booking.BookingId &&
                         x.ConferenceHallId == dto.ProposedHallId &&
                         (x.ApprovalStatus ==
                              (byte)BookingApprovalStatus.Pending ||
                          x.ApprovalStatus ==
                              (byte)BookingApprovalStatus.Approved) &&
                         proposedStart <
                             x.MeetingEndUtc.AddMinutes(x.CoolingMinutes) &&
                         occupiedEnd > x.MeetingStartUtc,
                    cancellationToken);

                if (bookingConflict)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "The proposed slot conflicts with another booking.");
                }

                var reservationConflict = await _db.HallReservations.AnyAsync(
                    x => x.ConferenceHallId == dto.ProposedHallId &&
                         x.ReservationStatus ==
                             (byte)ReservationStatus.Active &&
                         (!x.ExpiryAtUtc.HasValue ||
                          x.ExpiryAtUtc > DateTime.UtcNow) &&
                         (!booking.ReservationOccurrenceId.HasValue ||
                          x.ReservationOccurrenceId !=
                              booking.ReservationOccurrenceId.Value) &&
                         proposedStart < x.ReservationEndUtc &&
                         occupiedEnd > x.ReservationStartUtc,
                    cancellationToken);

                if (reservationConflict)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "The proposed slot is reserved.");
                }

                var holdConflict = await _db.HallSlotHolds.AnyAsync(
                    x => x.ConferenceHallId == dto.ProposedHallId &&
                         x.BookingId != booking.BookingId &&
                         x.HoldStatus == (byte)SlotHoldStatus.Active &&
                         x.HoldExpiresAtUtc > DateTime.UtcNow &&
                         proposedStart < x.HoldEndUtc &&
                         occupiedEnd > x.HoldStartUtc,
                    cancellationToken);

                if (holdConflict)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "The proposed slot is temporarily held by another modification request.");
                }

                // Capture replacement details only when supplied.
                var participantsJson = dto.ProposedParticipants == null
                    ? null
                    : JsonSerializer.Serialize(dto.ProposedParticipants);

                var facilitiesJson = dto.ProposedFacilities == null
                    ? null
                    : JsonSerializer.Serialize(dto.ProposedFacilities);

                // VC details are stored as UTF-8 JSON bytes, not encrypted.
                // Never log or return this payload.
                byte[]? vcDetailsBytes = null;

                if (dto.ProposedVCPlatformId.HasValue ||
                    dto.ProposedVCCredentials != null)
                {
                    if (!dto.ProposedVCPlatformId.HasValue)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Failure(400, "VC platform is required when changing VC details.");
                    }

                    var platformAvailable = await _db.HallVCPlatformMaps
                        .AnyAsync(
                            x => x.ConferenceHallId == dto.ProposedHallId &&
                                 x.VCPlatformId == dto.ProposedVCPlatformId.Value &&
                                 x.IsActive,
                            cancellationToken);

                    if (!platformAvailable)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Failure(400, "The selected VC platform is not available in the proposed hall.");
                    }

                    var vcPayload = new
                    {
                        VCPlatformId = dto.ProposedVCPlatformId,
                        Credentials = dto.ProposedVCCredentials
                    };

                    vcDetailsBytes = JsonSerializer.SerializeToUtf8Bytes(
                        vcPayload);
                }

                var request = new BookingModificationRequest
                {
                    BookingId = booking.BookingId,
                    RequestedByUserId = actorId,
                    RequestedByDepartmentId = profile.DepartmentId.Value,
                    ProposedHallId = dto.ProposedHallId,
                    ProposedStartUtc = proposedStart,
                    ProposedEndUtc = proposedEnd,
                    ProposedSubject = dto.ProposedSubject,
                    ProposedChairedBy = dto.ProposedChairedBy,
                    ProposedContactName = dto.ProposedContactName,
                    ProposedContactDesignation = dto.ProposedContactDesignation,
                    ProposedContactMobile = dto.ProposedContactMobile,
                    ProposedParticipantsJson = participantsJson,
                    ProposedFacilitiesJson = facilitiesJson,
                    ProposedVCDetailsEncrypted = vcDetailsBytes,
                    ChangeReason = dto.ChangeReason.Trim(),
                    ModificationStatus = (byte)ModificationStatus.Pending,
                    HoldExpiresAtUtc = holdExpiry,
                    CreatedAtUtc = now
                };

                _db.BookingModificationRequests.Add(request);
                await _db.SaveChangesAsync(cancellationToken);

                var slotHold = new HallSlotHold
                {
                    ConferenceHallId = dto.ProposedHallId,
                    BookingId = booking.BookingId,
                    ModificationRequestId = request.ModificationRequestId,
                    HoldStartUtc = proposedStart,
                    HoldEndUtc = occupiedEnd,
                    HoldExpiresAtUtc = holdExpiry,
                    HoldStatus = (byte)SlotHoldStatus.Active,
                    CreatedAtUtc = now
                };

                _db.HallSlotHolds.Add(slotHold);

                _db.BookingAuditLogs.Add(new BookingAuditLog
                {
                    EntityType = "BookingModification",
                    EntityId = request.ModificationRequestId,
                    ActionType = "ModificationRequested",
                    PerformedByUserId = actorId,
                    RepresentedDepartmentId = profile.DepartmentId,
                    OldValuesJson = JsonSerializer.Serialize(new
                    {
                        booking.ConferenceHallId,
                        booking.MeetingStartUtc,
                        booking.MeetingEndUtc
                    }),
                    NewValuesJson = JsonSerializer.Serialize(new
                    {
                        dto.ProposedHallId,
                        ProposedStartUtc = proposedStart,
                        ProposedEndUtc = proposedEnd,
                        request.ProposedSubject
                    }),
                    Remarks = request.ChangeReason,
                    CreatedAtUtc = now
                });

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new ResponseDto
                {
                    IsSuccess = true,
                    StatusCode = 201,
                    Message = "Modification request submitted. The proposed slot is temporarily held pending approval.",
                    Result = new
                    {
                        request.ModificationRequestId,
                        request.BookingId,
                        request.ProposedHallId,
                        request.ProposedStartUtc,
                        request.ProposedEndUtc,
                        request.ModificationStatus,
                        request.HoldExpiresAtUtc
                    }
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                return Failure(500, "An unexpected error occurred while submitting the modification request.");
            }
        }

        public async Task<ResponseDto> GetMyRequests(
            CancellationToken cancellationToken = default)
        {
            if (!_currentUser.IsAuthenticated)
                return Failure(401, "Authentication is required.");

            var userId = _currentUser.UserId;

            var result = await _db.BookingModificationRequests
                .AsNoTracking()
                .Where(x => x.RequestedByUserId == userId)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new ModificationRequestViewDto
                {
                    ModificationRequestId = x.ModificationRequestId,
                    BookingId = x.BookingId,
                    RequestedByUserId = x.RequestedByUserId,
                    RequestedByDepartmentId = x.RequestedByDepartmentId,
                    ProposedHallId = x.ProposedHallId,
                    ProposedStartUtc = x.ProposedStartUtc,
                    ProposedEndUtc = x.ProposedEndUtc,
                    ProposedSubject = x.ProposedSubject,
                    ChangeReason = x.ChangeReason,
                    ModificationStatus = x.ModificationStatus,
                    HoldExpiresAtUtc = x.HoldExpiresAtUtc,
                    ReviewRemarks = x.ReviewRemarks
                })
                .ToListAsync(cancellationToken);

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Your modification requests were retrieved successfully.",
                Result = result
            };
        }

        public async Task<ResponseDto> Withdraw(
            long modificationRequestId,
            CancellationToken cancellationToken = default)
        {
            if (!_currentUser.IsAuthenticated)
                return Failure(401, "Authentication is required.");

            var actorId = _currentUser.UserId;

            var request = await _db.BookingModificationRequests
                .FirstOrDefaultAsync(
                    x => x.ModificationRequestId == modificationRequestId,
                    cancellationToken);

            if (request == null)
                return Failure(404, "Modification request not found.");

            if (request.RequestedByUserId != actorId &&
                !_currentUser.IsInRole("Admin"))
                return Failure(403, "You are not authorized to withdraw this request.");

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted, cancellationToken);

            try
            {
                var booking = await _db.Bookings
                    .FirstOrDefaultAsync(
                        x => x.BookingId == request.BookingId,
                        cancellationToken);

                if (booking == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(404, "Associated booking not found.");
                }

                var hallIds = new[]
                {
                    booking.ConferenceHallId,
                    request.ProposedHallId
                }.Distinct().OrderBy(x => x).ToArray();

                foreach (var hallId in hallIds)
                {
                    var lockResult = await AcquireHallLock(
                        hallId, cancellationToken);

                    if (lockResult < 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Failure(409, "Unable to acquire hall booking lock. Please retry.");
                    }
                }

                await _db.Entry(request).ReloadAsync(cancellationToken);

                if (request.ModificationStatus !=
                    (byte)ModificationStatus.Pending)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "Only pending modification requests can be withdrawn.");
                }

                var now = DateTime.UtcNow;

                request.ModificationStatus = (byte)ModificationStatus.Withdrawn;
                request.ReviewedByUserId = actorId;
                request.ReviewedAtUtc = now;
                request.ReviewRemarks = "Withdrawn by requester.";

                var hold = await _db.HallSlotHolds
                    .FirstOrDefaultAsync(
                        x => x.ModificationRequestId == modificationRequestId &&
                             x.HoldStatus == (byte)SlotHoldStatus.Active,
                        cancellationToken);

                if (hold != null)
                {
                    hold.HoldStatus = (byte)SlotHoldStatus.Released;
                    hold.ReleasedAtUtc = now;
                }

                _db.BookingAuditLogs.Add(new BookingAuditLog
                {
                    EntityType = "BookingModification",
                    EntityId = request.ModificationRequestId,
                    ActionType = "ModificationWithdrawn",
                    PerformedByUserId = actorId,
                    RepresentedDepartmentId =
                        await _currentUser.GetDepartmentIdAsync(cancellationToken),
                    Remarks = request.ChangeReason,
                    CreatedAtUtc = now
                });

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new ResponseDto
                {
                    IsSuccess = true,
                    StatusCode = 200,
                    Message = "Modification request withdrawn and proposed slot released.",
                    Result = new
                    {
                        request.ModificationRequestId,
                        request.ModificationStatus
                    }
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                return Failure(500, "An unexpected error occurred while withdrawing the modification request.");
            }
        }


        public async Task<ResponseDto> GetPendingRequests(
            CancellationToken cancellationToken = default)
        {
            if (!_currentUser.IsAuthenticated)
                return Failure(401, "Authentication is required.");

            var isAdmin = _currentUser.IsInRole("Admin");

            if (!isAdmin && !_currentUser.IsInRole("SUBADMIN"))
                return Failure(403, "You are not authorized to view pending modifications.");

            var now = DateTime.UtcNow;

            var query = _db.BookingModificationRequests
                .AsNoTracking()
                .Where(x =>
                    x.ModificationStatus == (byte)ModificationStatus.Pending &&
                    x.HoldExpiresAtUtc > now);

            if (!isAdmin)
            {
                var userId = _currentUser.UserId;

                var assignedHalls = _db.HallSubAdminMaps
                    .Where(x =>
                        x.UserId == userId &&
                        x.IsActive &&
                        x.EffectiveFromUtc <= now &&
                        (!x.EffectiveToUtc.HasValue ||
                         x.EffectiveToUtc >= now))
                    .Select(x => x.ConferenceHallId);

                query = query.Where(x =>
                    assignedHalls.Contains(x.ProposedHallId) &&
                    _db.Bookings.Any(b =>
                        b.BookingId == x.BookingId &&
                        assignedHalls.Contains(b.ConferenceHallId)));
            }

            var result = await query
                .OrderBy(x => x.HoldExpiresAtUtc)
                .Select(x => new
                {
                    x.ModificationRequestId,
                    x.BookingId,
                    x.RequestedByUserId,
                    x.RequestedByDepartmentId,
                    x.ProposedHallId,
                    x.ProposedStartUtc,
                    x.ProposedEndUtc,
                    x.ProposedSubject,
                    x.ChangeReason,
                    x.ModificationStatus,
                    x.CreatedAtUtc,
                    x.HoldExpiresAtUtc,
                    OriginalHallId = _db.Bookings
                        .Where(b => b.BookingId == x.BookingId)
                        .Select(b => b.ConferenceHallId)
                        .FirstOrDefault(),
                    OriginalStartUtc = _db.Bookings
                        .Where(b => b.BookingId == x.BookingId)
                        .Select(b => b.MeetingStartUtc)
                        .FirstOrDefault(),
                    OriginalEndUtc = _db.Bookings
                        .Where(b => b.BookingId == x.BookingId)
                        .Select(b => b.MeetingEndUtc)
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Pending modification requests retrieved successfully.",
                Result = result
            };
        }

        public async Task<ResponseDto> GetRequestById(
            long modificationRequestId,
            CancellationToken cancellationToken = default)
        {
            if (!_currentUser.IsAuthenticated)
                return Failure(401, "Authentication is required.");

            var request = await _db.BookingModificationRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.ModificationRequestId == modificationRequestId,
                    cancellationToken);

            if (request == null)
                return Failure(404, "Modification request not found.");

            var booking = await _db.Bookings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.BookingId == request.BookingId,
                    cancellationToken);

            if (booking == null)
                return Failure(404, "Associated booking not found.");

            var isAdmin = _currentUser.IsInRole("Admin");
            var isRequester = request.RequestedByUserId == _currentUser.UserId;

            var canReview = isAdmin ||
                (await IsAssignedSubAdmin(
                    booking.ConferenceHallId, cancellationToken) &&
                 await IsAssignedSubAdmin(
                    request.ProposedHallId, cancellationToken));

            if (!isRequester && !canReview)
                return Failure(403, "You are not authorized to view this request.");

            // Do not return the VC credential snapshot.
            return new ResponseDto
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Modification request retrieved successfully.",
                Result = new
                {
                    request.ModificationRequestId,
                    request.BookingId,
                    request.RequestedByUserId,
                    request.RequestedByDepartmentId,
                    request.ProposedHallId,
                    request.ProposedStartUtc,
                    request.ProposedEndUtc,
                    request.ProposedSubject,
                    request.ProposedChairedBy,
                    request.ProposedContactName,
                    request.ProposedContactDesignation,
                    request.ProposedContactMobile,
                    ProposedParticipants = request.ProposedParticipantsJson,
                    ProposedFacilities = request.ProposedFacilitiesJson,
                    HasProposedVCDetails =
                        request.ProposedVCDetailsEncrypted != null,
                    request.ChangeReason,
                    request.ModificationStatus,
                    request.HoldExpiresAtUtc,
                    request.ReviewRemarks,
                    OriginalBooking = new
                    {
                        booking.ConferenceHallId,
                        booking.MeetingStartUtc,
                        booking.MeetingEndUtc,
                        booking.MeetingSubject,
                        booking.ChairedBy,
                        booking.ResponsiblePersonName,
                        booking.ResponsiblePersonDesignation,
                        booking.ResponsiblePersonMobile
                    }
                }
            };
        }

        public async Task<ResponseDto> Approve(
            long modificationRequestId,
            ModificationReviewDto dto,
            CancellationToken cancellationToken = default)
        {
            if (!_currentUser.IsAuthenticated)
                return Failure(401, "Authentication is required.");

            var actorId = _currentUser.UserId;
            var isAdmin = _currentUser.IsInRole("Admin");

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted, cancellationToken);

            try
            {
                var request = await _db.BookingModificationRequests
                    .FirstOrDefaultAsync(
                        x => x.ModificationRequestId == modificationRequestId,
                        cancellationToken);

                if (request == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(404, "Modification request not found.");
                }

                var booking = await _db.Bookings
                    .FirstOrDefaultAsync(
                        x => x.BookingId == request.BookingId,
                        cancellationToken);

                if (booking == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(404, "Associated booking not found.");
                }

                if (!isAdmin)
                {
                    var canManageOriginal = await IsAssignedSubAdmin(
                        booking.ConferenceHallId, cancellationToken);

                    var canManageProposed = await IsAssignedSubAdmin(
                        request.ProposedHallId, cancellationToken);

                    if (!canManageOriginal || !canManageProposed)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Failure(403,
                            "You must be assigned SubAdmin for both the original and proposed halls.");
                    }
                }

                var hallIds = new[]
                {
            booking.ConferenceHallId,
            request.ProposedHallId
        }.Distinct().OrderBy(x => x).ToArray();

                foreach (var hallId in hallIds)
                {
                    var lockResult = await AcquireHallLock(
                        hallId, cancellationToken);

                    if (lockResult < 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Failure(409,
                            "Unable to acquire hall booking lock. Please retry.");
                    }
                }

                await _db.Entry(request).ReloadAsync(cancellationToken);
                await _db.Entry(booking).ReloadAsync(cancellationToken);

                var now = DateTime.UtcNow;

                if (request.ModificationStatus !=
                    (byte)ModificationStatus.Pending)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "This modification request is no longer pending.");
                }

                if (request.HoldExpiresAtUtc <= now)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "The modification request has expired. It cannot be approved.");
                }

                if (booking.ApprovalStatus !=
                        (byte)BookingApprovalStatus.Approved ||
                    booking.MeetingStatus != (byte)MeetingStatus.NotStarted ||
                    booking.MeetingStartUtc <= now)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409,
                        "The original booking is no longer eligible for modification.");
                }

                var hold = await _db.HallSlotHolds
                    .FirstOrDefaultAsync(
                        x => x.ModificationRequestId == modificationRequestId &&
                             x.HoldStatus == (byte)SlotHoldStatus.Active &&
                             x.HoldExpiresAtUtc > now,
                        cancellationToken);

                if (hold == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "The proposed slot hold is no longer active.");
                }

                var proposedHall = await _db.ConferenceHalls
                    .AnyAsync(
                        x => x.ConferenceHallId == request.ProposedHallId &&
                             x.IsActive,
                        cancellationToken);

                if (!proposedHall)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "The proposed hall is no longer active.");
                }

                var coolingMinutes = booking.CoolingMinutes > 0
                    ? booking.CoolingMinutes
                    : DefaultCoolingMinutes;

                var occupiedEnd = request.ProposedEndUtc
                    .AddMinutes(coolingMinutes);

                if (booking.ReservationOccurrenceId.HasValue)
                {
                    var occurrence = await _db.ReservationOccurrences
                        .FirstOrDefaultAsync(
                            x => x.ReservationOccurrenceId ==
                                 booking.ReservationOccurrenceId.Value &&
                                 x.OccurrenceStatus ==
                                 (byte)ReservationOccurrenceStatus.Booked,
                            cancellationToken);

                    if (occurrence == null ||
                        occurrence.ConferenceHallId != request.ProposedHallId ||
                        request.ProposedStartUtc < occurrence.OccurrenceStartUtc ||
                        occupiedEnd > occurrence.OccurrenceEndUtc)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Failure(409,
                            "The proposed period is outside the original reservation.");
                    }
                }

                var bookingConflict = await _db.Bookings.AnyAsync(
                    x => x.BookingId != booking.BookingId &&
                         x.ConferenceHallId == request.ProposedHallId &&
                         (x.ApprovalStatus ==
                              (byte)BookingApprovalStatus.Pending ||
                          x.ApprovalStatus ==
                              (byte)BookingApprovalStatus.Approved) &&
                         request.ProposedStartUtc <
                            x.MeetingEndUtc.AddMinutes(x.CoolingMinutes) &&
                         occupiedEnd > x.MeetingStartUtc,
                    cancellationToken);

                if (bookingConflict)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "The proposed slot now conflicts with another booking.");
                }

                var reservationConflict = await _db.HallReservations.AnyAsync(
                    x => x.ConferenceHallId == request.ProposedHallId &&
                         x.ReservationStatus == (byte)ReservationStatus.Active &&
                         (!x.ExpiryAtUtc.HasValue || x.ExpiryAtUtc > now) &&
                         (!booking.ReservationOccurrenceId.HasValue ||
                          x.ReservationOccurrenceId != booking.ReservationOccurrenceId) &&
                         request.ProposedStartUtc < x.ReservationEndUtc &&
                         occupiedEnd > x.ReservationStartUtc,
                    cancellationToken);

                if (reservationConflict)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "The proposed slot is now reserved.");
                }

                var otherHoldConflict = await _db.HallSlotHolds.AnyAsync(
                    x => x.SlotHoldId != hold.SlotHoldId &&
                         x.ConferenceHallId == request.ProposedHallId &&
                         x.HoldStatus == (byte)SlotHoldStatus.Active &&
                         x.HoldExpiresAtUtc > now &&
                         request.ProposedStartUtc < x.HoldEndUtc &&
                         occupiedEnd > x.HoldStartUtc,
                    cancellationToken);

                if (otherHoldConflict)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "Another active modification hold conflicts with this slot.");
                }

                // Apply the proposed booking details.
                var oldValues = new
                {
                    booking.ConferenceHallId,
                    booking.MeetingStartUtc,
                    booking.MeetingEndUtc,
                    booking.MeetingSubject,
                    booking.ChairedBy,
                    booking.ResponsiblePersonName,
                    booking.ResponsiblePersonDesignation,
                    booking.ResponsiblePersonMobile
                };

                booking.ConferenceHallId = request.ProposedHallId;
                booking.MeetingStartUtc = request.ProposedStartUtc;
                booking.MeetingEndUtc = request.ProposedEndUtc;

                if (request.ProposedSubject != null)
                    booking.MeetingSubject = request.ProposedSubject;

                if (request.ProposedChairedBy != null)
                    booking.ChairedBy = request.ProposedChairedBy;

                if (request.ProposedContactName != null)
                    booking.ResponsiblePersonName = request.ProposedContactName;

                if (request.ProposedContactDesignation != null)
                    booking.ResponsiblePersonDesignation =
                        request.ProposedContactDesignation;

                if (request.ProposedContactMobile != null)
                    booking.ResponsiblePersonMobile = request.ProposedContactMobile;

                booking.UpdatedAtUtc = now;
                booking.UpdatedByUserId = actorId;

                // null snapshot = retain existing participants.
                // [] snapshot = remove all existing participants.
                if (request.ProposedParticipantsJson != null)
                {
                    var participants =
                        JsonSerializer.Deserialize<List<BookingParticipantDto>>(
                            request.ProposedParticipantsJson,
                            new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            }) ?? new List<BookingParticipantDto>();

                    var existingParticipants = await _db.BookingParticipants
                        .Where(x => x.BookingId == booking.BookingId)
                        .ToListAsync(cancellationToken);

                    _db.BookingParticipants.RemoveRange(existingParticipants);

                    foreach (var p in participants)
                    {
                        _db.BookingParticipants.Add(new BookingParticipant
                        {
                            BookingId = booking.BookingId,
                            ParticipantUserId = p.ParticipantUserId,
                            ParticipantName = p.ParticipantName,
                            ParticipantDesignation = p.ParticipantDesignation,
                            ParticipantDepartmentId = p.ParticipantDepartmentId,
                            ParticipantEmail = p.ParticipantEmail,
                            ParticipantType = p.ParticipantType,
                            IsRequired = p.IsRequired,
                            AttendanceStatus = (byte)ParticipantAttendanceStatus.Unknown,
                            AddedByUserId = actorId,
                            AddedAtUtc = now,
                            IsActive = true
                        });
                    }
                }

                // null snapshot = retain existing facilities.
                // [] snapshot = remove all existing facilities.
                if (request.ProposedFacilitiesJson != null)
                {
                    var facilities =
                        JsonSerializer.Deserialize<List<BookingFacilityDto>>(
                            request.ProposedFacilitiesJson,
                            new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            }) ?? new List<BookingFacilityDto>();

                    var facilityIds = facilities
                        .Select(x => x.FacilityId)
                        .Distinct()
                        .ToList();

                    var validFacilities = await _db.HallFacilityMaps
                        .Where(x =>
                            x.ConferenceHallId == request.ProposedHallId &&
                            x.IsAvailable &&
                            facilityIds.Contains(x.FacilityId))
                        .Select(x => x.FacilityId)
                        .ToListAsync(cancellationToken);

                    if (validFacilities.Count != facilityIds.Count)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Failure(400,
                            "One or more proposed facilities are unavailable in the proposed hall.");
                    }

                    var existingFacilities = await _db.BookingFacilityMaps
                        .Where(x => x.BookingId == booking.BookingId)
                        .ToListAsync(cancellationToken);

                    _db.BookingFacilityMaps.RemoveRange(existingFacilities);

                    foreach (var f in facilities)
                    {
                        _db.BookingFacilityMaps.Add(new BookingFacilityMap
                        {
                            BookingId = booking.BookingId,
                            FacilityId = f.FacilityId,
                            IsRequested = f.IsRequested,
                            Remarks = f.Remarks
                        });
                    }
                }

                // VC snapshot is UTF-8 JSON, not encrypted.
                // Credentials are never returned, logged or audited.
                if (request.ProposedVCDetailsEncrypted != null)
                {
                    using var vcDocument = JsonDocument.Parse(
                        request.ProposedVCDetailsEncrypted);

                    var vcRoot = vcDocument.RootElement;

                    var vcPlatformId = vcRoot
                        .GetProperty("VCPlatformId")
                        .GetInt32();

                    var platformAvailable = await _db.HallVCPlatformMaps
                        .AnyAsync(
                            x => x.ConferenceHallId == request.ProposedHallId &&
                                 x.VCPlatformId == vcPlatformId &&
                                 x.IsActive,
                            cancellationToken);

                    if (!platformAvailable)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Failure(400,
                            "The proposed VC platform is not available in the proposed hall.");
                    }

                    var credentials = vcRoot.GetProperty("Credentials");

                    var vcDetails = await _db.BookingVCDetails
                        .FirstOrDefaultAsync(
                            x => x.BookingId == booking.BookingId,
                            cancellationToken);

                    if (vcDetails == null)
                    {
                        vcDetails = new BookingVCDetails
                        {
                            BookingId = booking.BookingId,
                            CreatedAtUtc = now
                        };
                        _db.BookingVCDetails.Add(vcDetails);
                    }

                    vcDetails.VCPlatformId = vcPlatformId;

                    if (credentials.ValueKind == JsonValueKind.Object)
                    {
                        var meetingId = credentials
                            .GetProperty("VCMeetingId").GetString() ?? "";

                        var password = credentials
                            .GetProperty("VCPassword").GetString() ?? "";

                        vcDetails.VCMeetingIdEncrypted =
                            Encoding.UTF8.GetBytes(meetingId);

                        vcDetails.VCPasswordEncrypted =
                            Encoding.UTF8.GetBytes(password);

                        vcDetails.EncryptionKeyVersion = "PLAIN";

                        if (credentials.TryGetProperty(
                            "VCInstructions", out var instructions))
                        {
                            vcDetails.VCInstructions =
                                instructions.GetString();
                        }
                    }

                    vcDetails.UpdatedAtUtc = now;
                }

                var departmentId =
                    await _currentUser.GetDepartmentIdAsync(cancellationToken);

                var oldModificationStatus = request.ModificationStatus;

                request.ModificationStatus = (byte)ModificationStatus.Approved;
                request.ReviewedByUserId = actorId;
                request.ReviewedAtUtc = now;
                request.ReviewRemarks = dto.ReviewRemarks;

                hold.HoldStatus = (byte)SlotHoldStatus.Released;
                hold.ReleasedAtUtc = now;

                _db.BookingStatusHistory.Add(new BookingStatusHistory
                {
                    BookingId = booking.BookingId,
                    OldApprovalStatus = booking.ApprovalStatus,
                    NewApprovalStatus = booking.ApprovalStatus,
                    OldMeetingStatus = booking.MeetingStatus,
                    NewMeetingStatus = booking.MeetingStatus,
                    ChangedByUserId = actorId,
                    RepresentedDepartmentId = departmentId,
                    Remarks = "Booking modified: " + request.ChangeReason,
                    ChangedAtUtc = now
                });

                _db.BookingAuditLogs.Add(new BookingAuditLog
                {
                    EntityType = "BookingModification",
                    EntityId = request.ModificationRequestId,
                    ActionType = "ModificationApproved",
                    PerformedByUserId = actorId,
                    RepresentedDepartmentId = departmentId,
                    OldValuesJson = JsonSerializer.Serialize(oldValues),
                    NewValuesJson = JsonSerializer.Serialize(new
                    {
                        booking.ConferenceHallId,
                        booking.MeetingStartUtc,
                        booking.MeetingEndUtc,
                        booking.MeetingSubject,
                        booking.ChairedBy
                    }),
                    Remarks = dto.ReviewRemarks ?? request.ChangeReason,
                    CreatedAtUtc = now
                });

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new ResponseDto
                {
                    IsSuccess = true,
                    StatusCode = 200,
                    Message = "Modification approved and the original booking updated successfully.",
                    Result = new
                    {
                        request.ModificationRequestId,
                        booking.BookingId,
                        booking.ConferenceHallId,
                        booking.MeetingStartUtc,
                        booking.MeetingEndUtc,
                        request.ModificationStatus,
                        request.ReviewedAtUtc
                    }
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                return Failure(500,
                    "An unexpected error occurred while approving the modification.");
            }
        }

        public async Task<ResponseDto> Reject(
            long modificationRequestId,
            ModificationReviewDto dto,
            CancellationToken cancellationToken = default)
        {
            if (!_currentUser.IsAuthenticated)
                return Failure(401, "Authentication is required.");

            var actorId = _currentUser.UserId;
            var isAdmin = _currentUser.IsInRole("Admin");

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted, cancellationToken);

            try
            {
                var request = await _db.BookingModificationRequests
                    .FirstOrDefaultAsync(
                        x => x.ModificationRequestId == modificationRequestId,
                        cancellationToken);

                if (request == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(404, "Modification request not found.");
                }

                var booking = await _db.Bookings
                    .FirstOrDefaultAsync(
                        x => x.BookingId == request.BookingId,
                        cancellationToken);

                if (booking == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(404, "Associated booking not found.");
                }

                if (!isAdmin)
                {
                    var canManageOriginal = await IsAssignedSubAdmin(
                        booking.ConferenceHallId, cancellationToken);

                    var canManageProposed = await IsAssignedSubAdmin(
                        request.ProposedHallId, cancellationToken);

                    if (!canManageOriginal || !canManageProposed)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Failure(403,
                            "You must be assigned SubAdmin for both halls.");
                    }
                }

                var hallIds = new[]
                {
            booking.ConferenceHallId,
            request.ProposedHallId
        }.Distinct().OrderBy(x => x).ToArray();

                foreach (var hallId in hallIds)
                {
                    var lockResult = await AcquireHallLock(
                        hallId, cancellationToken);

                    if (lockResult < 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Failure(409,
                            "Unable to acquire hall booking lock. Please retry.");
                    }
                }

                await _db.Entry(request).ReloadAsync(cancellationToken);

                if (request.ModificationStatus !=
                    (byte)ModificationStatus.Pending)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure(409, "Only pending modification requests can be rejected.");
                }

                var now = DateTime.UtcNow;

                request.ModificationStatus = (byte)ModificationStatus.Rejected;
                request.ReviewedByUserId = actorId;
                request.ReviewedAtUtc = now;
                request.ReviewRemarks = dto.ReviewRemarks;

                var hold = await _db.HallSlotHolds
                    .FirstOrDefaultAsync(
                        x => x.ModificationRequestId == modificationRequestId &&
                             x.HoldStatus == (byte)SlotHoldStatus.Active,
                        cancellationToken);

                if (hold != null)
                {
                    hold.HoldStatus = (byte)SlotHoldStatus.Released;
                    hold.ReleasedAtUtc = now;
                }

                var departmentId =
                    await _currentUser.GetDepartmentIdAsync(cancellationToken);

                _db.BookingAuditLogs.Add(new BookingAuditLog
                {
                    EntityType = "BookingModification",
                    EntityId = request.ModificationRequestId,
                    ActionType = "ModificationRejected",
                    PerformedByUserId = actorId,
                    RepresentedDepartmentId = departmentId,
                    Remarks = dto.ReviewRemarks ?? request.ChangeReason,
                    CreatedAtUtc = now
                });

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new ResponseDto
                {
                    IsSuccess = true,
                    StatusCode = 200,
                    Message = "Modification rejected. The original booking remains unchanged and the proposed slot has been released.",
                    Result = new
                    {
                        request.ModificationRequestId,
                        request.BookingId,
                        request.ModificationStatus,
                        request.ReviewedAtUtc
                    }
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                return Failure(500,
                    "An unexpected error occurred while rejecting the modification.");
            }
        }


        public async Task<int> ExpirePendingRequests(
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            var expiredRequestIds = await _db.BookingModificationRequests
                .AsNoTracking()
                .Where(x =>
                    x.ModificationStatus == (byte)ModificationStatus.Pending &&
                    x.HoldExpiresAtUtc <= now)
                .OrderBy(x => x.HoldExpiresAtUtc)
                .Select(x => x.ModificationRequestId)
                .Take(100)
                .ToListAsync(cancellationToken);

            var expiredCount = 0;

            foreach (var requestId in expiredRequestIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await using var transaction =
                    await _db.Database.BeginTransactionAsync(
                        IsolationLevel.ReadCommitted,
                        cancellationToken);

                try
                {
                    var request = await _db.BookingModificationRequests
                        .FirstOrDefaultAsync(
                            x => x.ModificationRequestId == requestId,
                            cancellationToken);

                    if (request == null ||
                        request.ModificationStatus !=
                            (byte)ModificationStatus.Pending ||
                        request.HoldExpiresAtUtc > DateTime.UtcNow)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        continue;
                    }

                    // Use the same lock resource as booking submission,
                    // approval, rejection and withdrawal.
                    var lockResult = await AcquireHallLock(
                        request.ProposedHallId,
                        cancellationToken);

                    if (lockResult < 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        continue;
                    }

                    // Recheck after acquiring the lock.
                    await _db.Entry(request).ReloadAsync(cancellationToken);

                    if (request.ModificationStatus !=
                            (byte)ModificationStatus.Pending ||
                        request.HoldExpiresAtUtc > DateTime.UtcNow)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        continue;
                    }

                    var currentTime = DateTime.UtcNow;

                    request.ModificationStatus =
                        (byte)ModificationStatus.Expired;

                    request.ReviewedAtUtc = currentTime;
                    request.ReviewRemarks =
                        "Automatically expired because the modification hold period elapsed.";

                    var activeHolds = await _db.HallSlotHolds
                        .Where(x =>
                            x.ModificationRequestId == requestId &&
                            x.HoldStatus == (byte)SlotHoldStatus.Active)
                        .ToListAsync(cancellationToken);

                    foreach (var hold in activeHolds)
                    {
                        hold.HoldStatus = (byte)SlotHoldStatus.Expired;
                        hold.ReleasedAtUtc = currentTime;
                    }

                    _db.BookingAuditLogs.Add(new BookingAuditLog
                    {
                        EntityType = "BookingModification",
                        EntityId = request.ModificationRequestId,
                        ActionType = "ModificationExpired",
                        PerformedByUserId = "SYSTEM",
                        RepresentedDepartmentId =
                            request.RequestedByDepartmentId,
                        OldValuesJson = System.Text.Json.JsonSerializer.Serialize(
                            new
                            {
                                ModificationStatus =
                                    (byte)ModificationStatus.Pending
                            }),
                        NewValuesJson = System.Text.Json.JsonSerializer.Serialize(
                            new
                            {
                                ModificationStatus =
                                    (byte)ModificationStatus.Expired,
                                ExpiredAtUtc = currentTime,
                                ReleasedHoldCount = activeHolds.Count
                            }),
                        Remarks = "Modification request expired automatically.",
                        CreatedAtUtc = currentTime
                    });

                    await _db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    expiredCount++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }

            return expiredCount;
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

            var result = await command.ExecuteScalarAsync(
                cancellationToken);

            return Convert.ToInt32(result);
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
                x => x.ConferenceHallId == hallId &&
                     x.UserId == userId &&
                     x.IsActive &&
                     x.EffectiveFromUtc <= now &&
                     (!x.EffectiveToUtc.HasValue ||
                      x.EffectiveToUtc >= now),
                cancellationToken);
        }

        private static ResponseDto Failure(
            int statusCode, string message)
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
