
using ConferenceBooking.Services.BookingAPI.Models.Dto;

namespace ConferenceBooking.Services.BookingAPI.Service.IService
{
    public interface IBookingModificationService
    {
        Task<ResponseDto> Submit(
            BookingModificationDto dto,
            CancellationToken cancellationToken = default);

        Task<ResponseDto> GetMyRequests(
            CancellationToken cancellationToken = default);

        Task<ResponseDto> Withdraw(
            long modificationRequestId,
            CancellationToken cancellationToken = default);
    }
}
