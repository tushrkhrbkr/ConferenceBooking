using static ConferenceBooking.Web.Utility.SD;

namespace ConferenceBooking.Web.Models
{
    public class RequestDto
    {
        public ApiType ApiType { get; set; } = ApiType.GET;
        public string Url { get; set; }
        public object Data { get; set; }
        public string AccessToken { get; set; }
        public IFormFile File { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
    }
}
