using System.ComponentModel.DataAnnotations;

namespace ConferenceBooking.Services.AuthAPI.Models
{
    public class UserProfile
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Designation { get; set; } = string.Empty;

        public string EmailAddress { get; set; } = string.Empty;

        public string MobileNumber { get; set; } = string.Empty;

        public int? DepartmentId { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? UpdatedAtUtc { get; set; }

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    }
}
