namespace ConferenceBooking.Web.Models.AuthLogin
{
    public class DepartmentOptionDto
    {
        public int DepartmentId { get; set; }
        public string DepartmentCode { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
    }
}