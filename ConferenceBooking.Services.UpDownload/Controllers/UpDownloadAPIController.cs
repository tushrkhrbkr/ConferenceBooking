using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ConferenceBooking.Services.UpDownload.Data;
using ConferenceBooking.Services.UpDownload.Models;
using ConferenceBooking.Services.UpDownload.Models.Dto;
using System.Threading;

namespace ConferenceBooking.Services.UpDownload.Controllers
{
    [Route("api/updownload")]
    [ApiController]
    [Authorize]
    public class UpDownloadAPIController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IMapper _mapper;
        private readonly ResponseDto _response;
        
        public UpDownloadAPIController(ApplicationDbContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
            _response = new();
        }

        [HttpPost]
        [Route("UploadFeedbackDocs")]
        public ResponseDto UploadFeedbackDocs([FromBody] List<FeedbackDocumentsDto> docList)
        {
            try
            {
                if (docList.Count != 0)
                {
                    var DocListData = docList.Select(docSingle => new FeedbackDocuments
                    {   
                        ReferenceID=docSingle.ReferenceID,
                        DocumentData= docSingle.DocumentData,
                        DocumentName = docSingle.DocumentName,
                        DocumentType= docSingle.DocumentType,
                        UploadedBy = docSingle.UploadedBy,
                        UploadedOn = docSingle.UploadedOn,
                        ReferenceTable = docSingle.ReferenceTable,
 
                    });
                    //var distinctUpdatedModel = updatedModel.DistinctBy(x => x.Place_id);
                    _db.Tbl_Feedback_DocumentRepository.AddRange(DocListData);
                    _db.SaveChanges();
                }

            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

        [HttpPost]
        [Route("UploadReportDocs")]
        public ResponseDto UploadReportDocs([FromBody] List<ReportDocumentsDto> docList)
        {
            try
            {
                if (docList.Count != 0)
                {
                    var DocListData = docList.Select(docSingle => new ReportDocuments
                    {
                        ReferenceID = docSingle.ReferenceID,
                        DocumentData = docSingle.DocumentData,
                        DocumentName = docSingle.DocumentName,
                        DocumentType = docSingle.DocumentType,
                        UploadedBy = docSingle.UploadedBy,
                        UploadedOn = docSingle.UploadedOn,
                        ReferenceTable = docSingle.ReferenceTable,

                    });
                    //var distinctUpdatedModel = updatedModel.DistinctBy(x => x.Place_id);
                    _db.Tbl_Report_DocumentRepository.AddRange(DocListData);
                    _db.SaveChanges();
                }

            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

        [HttpGet]
        [Route("getFeedbackDocument/{referenceid}")]
        public ResponseDto GetFeedbackDocument(long referenceid)
        {
            try
            {
                List<FeedbackDocuments> docObj = _db.Tbl_Feedback_DocumentRepository.Where(u => u.ReferenceID == referenceid).ToList();
                //var result = (from d in docObj
                //              join dt in _db.Tbl_Master_DocumentType on d.DocumentType equals dt.DocType_ID into md
                //              from abc in md.DefaultIfEmpty()
                //              select new DocumentDetailsDto
                //              {
                //                  Doc_ID = d.Doc_ID,
                //                  ProfileID = d.ProfileID,
                //                  DocumentName = d.DocumentName,
                //                  DocumentData = d.DocumentData,
                //                  DocumentType = abc != null ? abc.DocType_Name : "Template"
                //              }).ToList();

                _response.Result = docObj;
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

        [HttpGet]
        [Route("getReportDocument/{referenceid}")]
        public ResponseDto GetReportDocument(long referenceid)
        {
            try
            {
                List<ReportDocuments> docObj = _db.Tbl_Report_DocumentRepository.Where(u => u.ReferenceID == referenceid).ToList();
                //var result = (from d in docObj
                //              join dt in _db.Tbl_Master_DocumentType on d.DocumentType equals dt.DocType_ID into md
                //              from abc in md.DefaultIfEmpty()
                //              select new DocumentDetailsDto
                //              {
                //                  Doc_ID = d.Doc_ID,
                //                  ProfileID = d.ProfileID,
                //                  DocumentName = d.DocumentName,
                //                  DocumentData = d.DocumentData,
                //                  DocumentType = abc != null ? abc.DocType_Name : "Template"
                //              }).ToList();

                _response.Result = docObj;
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

    }
}
