namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{
    public class RegistrationLookupsDto
    {
        public List<DepartmentOptionDto> Departments { get; set; } = new();

        public List<RoleOptionDto> Roles { get; set; } = new();
    }

    public class DepartmentOptionDto
    {
        public int DepartmentId { get; set; }

        public string DepartmentName { get; set; } = string.Empty;
    }

    public class RoleOptionDto
    {
        public string RoleName { get; set; } = string.Empty;
    }
}