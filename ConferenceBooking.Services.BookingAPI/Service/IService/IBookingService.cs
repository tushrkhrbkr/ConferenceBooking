using ConferenceBooking.Services.BookingAPI.Models.Dto;

namespace ConferenceBooking.Services.BookingAPI.Service.IService
{
    public interface IBookingService
    {
        Task<ResponseDto> CreateBooking(
            CreateBookingDto dto,
            CancellationToken cancellationToken = default);

        Task<ResponseDto> CheckAvailability(
            BookingAvailabilityQueryDto dto,
            CancellationToken cancellationToken = default);
    }
}