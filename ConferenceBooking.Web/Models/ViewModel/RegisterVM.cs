using Microsoft.AspNetCore.Mvc.Rendering;
using ConferenceBooking.Web.Models.AuthLogin;

namespace MUS.Webapp.Models.ViewModel
{
    public class RegisterVM
    {
        //public ADRegistrationRequestDto registrationRequestlist { get; set; }
        public UserProfileRegistrationDto userProfileRegistrationlist { get; set; }
        public bool FirstFormSubmitted { get; set; }
        public string? UserID { get; set; }
        public string? DisplayName { get; set; }
    }
}
