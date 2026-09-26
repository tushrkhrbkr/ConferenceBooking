namespace ConferenceBooking.Web.Models.AuthLogin
{
    public class UserProfileRegistrationDto
    {
        public int ID { get; set; }
        public string? User_ID { get; set; }

        public string? User_Agency { get; set; }
        public string? User_Agency_Type { get; set; }
        public string? User_State { get; set; }
        //public string? User_State_GeoID { get; set; }
        public string? User_Parent_ID { get; set; }
        public int Is_Active { get; set; }
        public int Delete_Flag { get; set; }
        public bool Is_Parent { get; set; }
        public string? GroupHead_User_ID { get; set; }
        public bool Is_GroupHead { get; set; }
        public string? User_District { get; set; }
        public bool Is_DirectSharing { get; set; }
    }
}
