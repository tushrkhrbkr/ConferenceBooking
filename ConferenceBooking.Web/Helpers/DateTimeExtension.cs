namespace ConferenceBooking.Web.Helpers
{
    public static class DateTimeExtension
    {
        public static string ToTimeAgo(this DateTime? dateTime)
        {
            if (!dateTime.HasValue)
                return "Not available";

            var timeSpan = DateTime.Now - dateTime.Value;

            if (timeSpan.TotalSeconds < 60)
                return "Just Now";

            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} minute(s) ago";

            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} hour(s) ago";

            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays} day(s) ago";

            if (timeSpan.TotalDays < 30)
                return $"{(int)(timeSpan.TotalDays / 7)} week(s) ago";

            if (timeSpan.TotalDays < 365)
                return $"{(int)(timeSpan.TotalDays / 30)} month(s) ago";

            return $"{(int)(timeSpan.TotalDays / 365)} year(s) ago";
        }

        public static string GetBadgeClass(this DateTime? dateTime)
        {
            if (dateTime == null)
                return "badge-soft-dark"; // grey for NA

            var diff = DateTime.Now - dateTime.Value;

            if (diff.TotalDays <= 3)
                return "badge-soft-success";   // green

            if (diff.TotalDays <= 7)
                return "badge-soft-warning";   // amber

            return "badge-soft-danger";        // red
        }
    }
}
