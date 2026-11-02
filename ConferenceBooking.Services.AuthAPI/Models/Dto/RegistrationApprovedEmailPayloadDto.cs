namespace ConferenceBooking.Services.AuthAPI.Models.Dto
{
    public class RegistrationApprovedEmailPayloadDto
    {
        public string UserName { get; set; } = string.Empty;
        public string ApplicationReference { get; set; } = string.Empty;
        public string ReviewedOn { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }
}