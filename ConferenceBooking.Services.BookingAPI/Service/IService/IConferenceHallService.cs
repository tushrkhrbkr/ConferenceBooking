using ConferenceBooking.Services.BookingAPI.Models.Dto;

namespace ConferenceBooking.Services.BookingAPI.Service.IService
{
    public interface IConferenceHallService
    {
        Task<ResponseDto> GetAll(
            bool activeOnly = true,
            CancellationToken cancellationToken = default);

        Task<ResponseDto> GetById(
            int id,
            CancellationToken cancellationToken = default);

        Task<ResponseDto> Create(
            CreateHallDto dto,
            CancellationToken cancellationToken = default);

        Task<ResponseDto> Update(
            int id,
            UpdateHallDto dto,
            CancellationToken cancellationToken = default);
    }
}
