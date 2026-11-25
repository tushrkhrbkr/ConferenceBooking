using ConferenceBooking.Web.Models;
using ConferenceBooking.Web.Service.IService;
using ConferenceBooking.Web.Utility;

namespace ConferenceBooking.Web.Service
{
    public class UpDownloadService : IUpDownloadService
    {
        private readonly IBaseService _baseService;
        public UpDownloadService(IBaseService baseService)
        {
            _baseService = baseService;
        }

        public async Task<ResponseDto?> UploadReportDocument(List<ReportDocumentsDto> docList)
        {
            return await _baseService.SendAsync(new RequestDto()
            {
                ApiType = SD.ApiType.POST,
                Url = SD.UpDownloadAPIBase + "/api/updownload/UploadReportDocs",
                Data = docList
            });
        }

        public async Task<ResponseDto?> UploadFeedbackDocument(List<FeedbackDocumentsDto> docList)
        {
            return await _baseService.SendAsync(new RequestDto()
            {
                ApiType = SD.ApiType.POST,
                Url = SD.UpDownloadAPIBase + "/api/updownload/UploadFeedbackDocs",
                Data = docList
            });
        }


        public async Task<ResponseDto?> GetReportDocument(int referenceid)
        {
            return await _baseService.SendAsync(new RequestDto()
            {
                ApiType = SD.ApiType.GET,
                Url = SD.UpDownloadAPIBase + "/api/updownload/getReportDocument/" + referenceid
            });
        }

        public async Task<ResponseDto?> GetFeedbackDocument(int referenceid)
        {
            return await _baseService.SendAsync(new RequestDto()
            {
                ApiType = SD.ApiType.GET,
                Url = SD.UpDownloadAPIBase + "/api/updownload/getFeedbackDocument/" + referenceid
            });
        }
    }
}
