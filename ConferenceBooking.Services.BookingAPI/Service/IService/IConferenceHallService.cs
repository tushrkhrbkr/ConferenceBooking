using ConferenceBooking.Services.BookingAPI.Models.Dto;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceBooking.Services.BookingAPI.Service.IService
{
    public interface IConferenceHallService
    {
        Task<ResponseDto> Create(CreateHallDto dto, CancellationToken cancellationToken);
        Task<ResponseDto> GetAll(bool activeOnly, CancellationToken cancellationToken);
        Task<ResponseDto> GetById(int id, CancellationToken cancellationToken);
        Task<ResponseDto> Update(int id, UpdateHallDto dto, CancellationToken cancellationToken);

    }
}
