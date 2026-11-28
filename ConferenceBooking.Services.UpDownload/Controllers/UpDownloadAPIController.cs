
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConferenceBooking.Services.UpDownload.Data;
using ConferenceBooking.Services.UpDownload.Models;
using ConferenceBooking.Services.UpDownload.Models.Dto;
using ConferenceBooking.Services.UpDownload.Services;
using System.Security.Claims;

namespace ConferenceBooking.Services.UpDownload.Controllers
{
    [Route("api/updownload")]
    [ApiController]
    [Authorize]
    public class UpDownloadAPIController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly DocumentUploadValidator _validator;
        private readonly ILogger<UpDownloadAPIController> _logger;

        public UpDownloadAPIController(
            ApplicationDbContext db,
            DocumentUploadValidator validator,
            ILogger<UpDownloadAPIController> logger)
        {
            _db = db;
            _validator = validator;
            _logger = logger;
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("nameid");
        }

        [HttpPost]
        [Route("UploadFeedbackDocs")]
        public ActionResult<ResponseDto> UploadFeedbackDocs(
            [FromBody] List<FeedbackDocumentsDto>? docList)
        {
            var response = new ResponseDto();

            try
            {
                if (docList == null || docList.Count == 0)
                {
                    response.IsSuccess = false;
                    response.Message = "No documents were provided.";
                    return BadRequest(response);
                }

                var uploadedBy = GetCurrentUserId();

                if (string.IsNullOrWhiteSpace(uploadedBy))
                {
                    response.IsSuccess = false;
                    response.Message = "User identity could not be determined from the authentication token.";
                    return Unauthorized(response);
                }

                foreach (var doc in docList)
                {
                    var validation = _validator.Validate(
                        doc.DocumentName,
                        doc.DocumentData,
                        doc.ReferenceTable,
                        doc.ReferenceID);

                    if (!validation.IsValid)
                    {
                        response.IsSuccess = false;
                        response.Message = validation.Message;
                        return BadRequest(response);
                    }
                }

                var documents = docList.Select(doc =>
                    new FeedbackDocuments
                    {
                        ReferenceID = doc.ReferenceID,
                        DocumentData = doc.DocumentData,
                        DocumentName = doc.DocumentName,
                        DocumentType = doc.DocumentType,
                        ReferenceTable = doc.ReferenceTable,
                        UploadedBy = uploadedBy,
                        UploadedOn = DateTime.UtcNow
                    }).ToList();

                _db.Tbl_Feedback_DocumentRepository.AddRange(documents);
                _db.SaveChanges();

                response.IsSuccess = true;
                response.Message = "Feedback documents uploaded successfully.";
                response.Result = documents.Select(x => new
                {
                    x.DocumentID,
                    x.DocumentAutoID,
                    x.DocumentName,
                    x.ReferenceID,
                    x.UploadedOn
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading feedback documents.");

                response.IsSuccess = false;
                response.Message = "An error occurred while uploading feedback documents.";

                return StatusCode(500, response);
            }
        }

        [HttpPost]
        [Route("UploadReportDocs")]
        public ActionResult<ResponseDto> UploadReportDocs(
            [FromBody] List<ReportDocumentsDto>? docList)
        {
            var response = new ResponseDto();

            try
            {
                if (docList == null || docList.Count == 0)
                {
                    response.IsSuccess = false;
                    response.Message = "No documents were provided.";
                    return BadRequest(response);
                }

                var uploadedBy = GetCurrentUserId();

                if (string.IsNullOrWhiteSpace(uploadedBy))
                {
                    response.IsSuccess = false;
                    response.Message = "User identity could not be determined from the authentication token.";
                    return Unauthorized(response);
                }

                foreach (var doc in docList)
                {
                    var validation = _validator.Validate(
                        doc.DocumentName,
                        doc.DocumentData,
                        doc.ReferenceTable,
                        doc.ReferenceID);

                    if (!validation.IsValid)
                    {
                        response.IsSuccess = false;
                        response.Message = validation.Message;
                        return BadRequest(response);
                    }
                }

                var documents = docList.Select(doc =>
                    new ReportDocuments
                    {
                        ReferenceID = doc.ReferenceID,
                        DocumentData = doc.DocumentData,
                        DocumentName = doc.DocumentName,
                        DocumentType = doc.DocumentType,
                        ReferenceTable = doc.ReferenceTable,
                        UploadedBy = uploadedBy,
                        UploadedOn = DateTime.UtcNow
                    }).ToList();

                _db.Tbl_Report_DocumentRepository.AddRange(documents);
                _db.SaveChanges();

                response.IsSuccess = true;
                response.Message = "Report documents uploaded successfully.";
                response.Result = documents.Select(x => new
                {
                    x.DocumentID,
                    x.DocumentAutoID,
                    x.DocumentName,
                    x.ReferenceID,
                    x.UploadedOn
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading report documents.");

                response.IsSuccess = false;
                response.Message = "An error occurred while uploading report documents.";

                return StatusCode(500, response);
            }
        }

        [HttpGet]
        [Route("getFeedbackDocument/{referenceid}")]
        public ActionResult<ResponseDto> GetFeedbackDocument(long referenceid)
        {
            var response = new ResponseDto();

            try
            {
                if (referenceid <= 0)
                {
                    response.IsSuccess = false;
                    response.Message = "Invalid reference ID.";
                    return BadRequest(response);
                }

                var documents = _db.Tbl_Feedback_DocumentRepository
                    .Where(x => x.ReferenceID == referenceid)
                    .ToList();

                response.Result = documents;
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feedback documents.");

                response.IsSuccess = false;
                response.Message = "An error occurred while retrieving feedback documents.";

                return StatusCode(500, response);
            }
        }

        [HttpGet]
        [Route("getReportDocument/{referenceid}")]
        public ActionResult<ResponseDto> GetReportDocument(long referenceid)
        {
            var response = new ResponseDto();

            try
            {
                if (referenceid <= 0)
                {
                    response.IsSuccess = false;
                    response.Message = "Invalid reference ID.";
                    return BadRequest(response);
                }

                var documents = _db.Tbl_Report_DocumentRepository
                    .Where(x => x.ReferenceID == referenceid)
                    .ToList();

                response.Result = documents;
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving report documents.");

                response.IsSuccess = false;
                response.Message = "An error occurred while retrieving report documents.";

                return StatusCode(500, response);
            }
        }
    }
}
