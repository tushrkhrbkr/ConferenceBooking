namespace ConferenceBooking.Services.AuthAPI.Enums
{
    public static class PasswordResetStatus
    {
        public const string InvalidRequest = "INVALID_REQUEST";

        public const string UserNotFound = "USER_NOT_FOUND";

        public const string RegistrationPending = "REGISTRATION_PENDING";

        public const string UserInactive = "USER_INACTIVE";

        public const string DepartmentInactive = "DEPARTMENT_INACTIVE";

        public const string AccountLocked = "ACCOUNT_LOCKED";

        public const string OtpQueued = "OTP_QUEUED";

        public const string OtpCooldown = "OTP_COOLDOWN";

        public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";

        public const string EmailQueueFailed = "EMAIL_QUEUE_FAILED";
    }
}