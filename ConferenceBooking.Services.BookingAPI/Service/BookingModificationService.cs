
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
            parameter.Value = $"CONVENE_HALL_{hallId}";
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
