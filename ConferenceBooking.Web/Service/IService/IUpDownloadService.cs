
using ConferenceBooking.Web.Models;

namespace ConferenceBooking.Web.Service.IService
{
    public interface IUpDownloadService
    {
        Task<ResponseDto?> GetReportDocument(int referenceid);

        Task<ResponseDto?> GetFeedbackDocument(int referenceid);

        Task<ResponseDto?> UploadReportDocument(
            List<ReportDocumentsDto> docList);

        Task<ResponseDto?> UploadFeedbackDocument(
            List<FeedbackDocumentsDto> docList);

        Task<ResponseDto?> GetReportDocumentMetadata(
            long referenceid,
            string referenceTable);

        Task<ResponseDto?> GetFeedbackDocumentMetadata(
            long referenceid,
            string referenceTable);

        Task<ResponseDto?> GetFeedbackDocumentById(Guid documentId);

        Task<ResponseDto?> GetReportDocumentById(Guid documentId);
    }
}
