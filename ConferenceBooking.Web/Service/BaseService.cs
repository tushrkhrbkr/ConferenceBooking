using ConferenceBooking.Web.Service.IService;
using static ConferenceBooking.Web.Utility.SD;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using ConferenceBooking.Web.Models;

namespace ConferenceBooking.Web.Service
{
    public class BaseService : IBaseService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ITokenProvider _tokenProvider;
        public BaseService(IHttpClientFactory httpClientFactory, ITokenProvider tokenProvider)
        {
            _httpClientFactory = httpClientFactory;
            _tokenProvider = tokenProvider;
        }
        public async Task<ResponseDto?> SendAsync(RequestDto requestDto, bool withBearer = true)
        {
            try
            {
                HttpClient client = _httpClientFactory.CreateClient("musAPI");
                client.Timeout = TimeSpan.FromMinutes(10);
                HttpRequestMessage message = new();
                message.Headers.Add("Accept", "application/json");
                //token

                if (withBearer)
                {
                    var token = _tokenProvider.GetToken();
                    if (token == null)
                    {
                        message.Headers.Add("Authorization", $"Bearer {requestDto.AccessToken}");
                    }
                    else
                    {
                        message.Headers.Add("Authorization", $"Bearer {token}");
                    }

                }

                message.RequestUri = new Uri(requestDto.Url);
                if (requestDto.Data != null)
                {
                    message.Content = new StringContent(JsonConvert.SerializeObject(requestDto.Data), Encoding.UTF8, "application/json");
                }
                else if (requestDto.File != null)
                {
                    var form = new MultipartFormDataContent();
                    var stream = requestDto.File.OpenReadStream();
                    var streamContent = new StreamContent(stream);
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(requestDto.File.ContentType);
                    form.Add(streamContent, "file", requestDto.File.FileName);
                    message.Content = form;
                }
                
                HttpResponseMessage? apiResponse = null;

                switch (requestDto.ApiType)
                {
                    case ApiType.POST:
                        message.Method = HttpMethod.Post;
                        break;
                    case ApiType.PUT:
                        message.Method = HttpMethod.Put;
                        break;
                    case ApiType.DELETE:
                        message.Method = HttpMethod.Delete;
                        break;
                    default:
                        message.Method = HttpMethod.Get;
                        break;
                }
                apiResponse = await client.SendAsync(message);
                switch (apiResponse.StatusCode)
                {
                    case HttpStatusCode.NotFound:
                        return new() { IsSuccess = false, Message = "Not Found" };

                    case HttpStatusCode.Forbidden:
                        return new() { IsSuccess = false, Message = "Access Denied" };

                    case HttpStatusCode.Unauthorized:
                        return new() { IsSuccess = false, Message = "Unauthorized" };

                    case HttpStatusCode.InternalServerError:
                        return new() { IsSuccess = false, Message = "Internal Server Error" };

                    default:
                        var apiContent = await apiResponse.Content.ReadAsStringAsync();
                        var apiResponseDto = JsonConvert.DeserializeObject<ResponseDto>(apiContent);
                        return apiResponseDto;
                }
            }
            catch (Exception ex)
            {
                var dto = new ResponseDto
                {
                    Message = ex.Message.ToString(),
                    IsSuccess = false
                };
                return dto;
            }
        }
    }
}
