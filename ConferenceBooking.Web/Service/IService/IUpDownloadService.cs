using ConferenceBooking.Web.Models;

namespace ConferenceBooking.Web.Service.IService
{
    public interface IUpDownloadService
    {
        Task<ResponseDto?> GetReportDocument(int referenceid);

        Task<ResponseDto?> GetFeedbackDocument(int referenceid);

        Task<ResponseDto?> UploadReportDocument(List<ReportDocumentsDto> docList);

        Task<ResponseDto?> UploadFeedbackDocument(List<FeedbackDocumentsDto> docList);
    }
}
