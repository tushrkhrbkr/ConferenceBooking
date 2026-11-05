using ConferenceBooking.Services.BookingAPI.Models.Dto;

namespace ConferenceBooking.Services.BookingAPI.Service.IService
{
    public interface IVCPlatformService
    {
        Task<ResponseDto> GetAll(
            bool activeOnly = true,
            CancellationToken cancellationToken = default);

        Task<ResponseDto> GetById(
            int id,
            CancellationToken cancellationToken = default);

        Task<ResponseDto> Create(
            CreateVCPlatformDto dto,
            CancellationToken cancellationToken = default);

        Task<ResponseDto> Update(
            int id,
            UpdateVCPlatformDto dto,
            CancellationToken cancellationToken = default);

        Task<ResponseDto> GetHallVCPlatforms(
            int hallId,
            bool activeOnly = true,
            CancellationToken cancellationToken = default);

        Task<ResponseDto> MapVCPlatformToHall(
            int hallId,
            HallVCPlatformSelectionDto dto,
            CancellationToken cancellationToken = default);

        Task<ResponseDto> UnmapVCPlatformFromHall(
            int hallId,
            int vcPlatformId,
            CancellationToken cancellationToken = default);
    }
}