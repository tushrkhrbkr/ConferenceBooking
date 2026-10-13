namespace ConferenceBooking.Web.Models.AuthLogin
{
    public class RegistrationLookupsDto
    {
        public List<DepartmentOptionDto> Departments { get; set; } = new();

        public List<RoleOptionDto> Roles { get; set; } = new();
    }
}