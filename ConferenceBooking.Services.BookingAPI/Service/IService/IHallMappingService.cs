using ConferenceBooking.Services.BookingAPI.Models.Dto;

namespace ConferenceBooking.Services.BookingAPI.Service.IService
{
    public interface IHallMappingService
    {
        Task<ResponseDto> GetHallManagers(int hallId, CancellationToken ct = default);
        Task<ResponseDto> AssignHallManager(HallUserMappingDto dto, CancellationToken ct = default);
        Task<ResponseDto> RemoveHallManager(int hallId, string userId, CancellationToken ct = default);

        Task<ResponseDto> GetHallSubAdmins(int hallId, CancellationToken ct = default);
        Task<ResponseDto> AssignHallSubAdmin(HallUserMappingDto dto, CancellationToken ct = default);
        Task<ResponseDto> RemoveHallSubAdmin(int hallId, string userId, CancellationToken ct = default);

        Task<ResponseDto> GetHallDepartments(int hallId, CancellationToken ct = default);
        Task<ResponseDto> MapDepartmentToHall(HallDepartmentMappingDto dto, CancellationToken ct = default);
        Task<ResponseDto> RemoveDepartmentFromHall(int hallId, int departmentId, CancellationToken ct = default);

        Task<ResponseDto> GetRemoteUserHalls(string userId, CancellationToken ct = default);
        Task<ResponseDto> MapRemoteUserToHall(RemoteUserHallMappingDto dto, CancellationToken ct = default);
        Task<ResponseDto> RemoveRemoteUserFromHall(int hallId, string userId, CancellationToken ct = default);
    }
}