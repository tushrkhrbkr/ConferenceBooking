
using ConferenceBooking.Services.BookingAPI.Models.Dto;

namespace ConferenceBooking.Services.BookingAPI.Service.IService
{
    public interface IFacilityService
    {
        Task<ResponseDto> GetAll(
            bool activeOnly = true,
            CancellationToken cancellationToken = default);

        Task<ResponseDto> GetById(
            int id,
            CancellationToken cancellationToken = default);

        Task<ResponseDto> Create(
            CreateFacilityDto dto,
            CancellationToken cancellationToken = default);

        Task<ResponseDto> Update(
            int id,
            UpdateFacilityDto dto,
            CancellationToken cancellationToken = default);

        Task<ResponseDto> GetHallFacilities(
            int hallId,
            bool availableOnly = true,
            CancellationToken cancellationToken = default);

        Task<ResponseDto> MapFacilityToHall(
            int hallId,
            HallFacilitySelectionDto dto,
            CancellationToken cancellationToken = default);

        Task<ResponseDto> UnmapFacilityFromHall(
            int hallId,
            int facilityId,
            CancellationToken cancellationToken = default);
    }
}
