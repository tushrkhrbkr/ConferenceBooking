namespace ConferenceBooking.Services.AuthAPI.Utility
{
    public class SD
    {
        public const string RoleAdmin = "Admin";
        public const string RoleSubAdmin = "SubAdmin";
        public const string RoleHallManager = "HallManager";
        public const string RoleLocalUser = "LocalUser";
        public const string RoleRemoteUser = "RemoteUser";

        public static readonly string[] ValidRoles =
        {
            RoleAdmin,
            RoleSubAdmin,
            RoleHallManager,
            RoleLocalUser,
            RoleRemoteUser
        };

    }
}
