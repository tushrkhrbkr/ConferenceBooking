
using Azure;
using ConferenceBooking.Services.UpDownload.Data;
using ConferenceBooking.Services.UpDownload.Models;
using ConferenceBooking.Services.UpDownload.Models.Dto;
using ConferenceBooking.Services.UpDownload.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        [HttpGet]
        [Route("DownloadFeedbackDocument/{documentId:guid}")]
        public ActionResult<ResponseDto> DownloadFeedbackDocument(Guid documentId)
        {
            var response = new ResponseDto();
            try
            {
                var document = _db.Tbl_Feedback_DocumentRepository
                    .FirstOrDefault(x => x.DocumentID == documentId);

                if (document == null)
                    return NotFound("Feedback document not found.");

                if (document.DocumentData == null ||
                    document.DocumentData.Length == 0)
                    return NotFound("Document data is unavailable.");

                //var fileName = Path.GetFileName(document.DocumentName ?? "document");

                //var contentType = GetContentType(fileName);

                //return File(
                //    document.DocumentData,
                //    contentType,
                //    fileName);
                response.Result = document;
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error downloading feedback document {DocumentId}",
                    documentId);
                response.IsSuccess = false;
                response.Message = "An error occurred while downloading the document.";

                return StatusCode(500, response);
               
            }
        }

        [HttpGet]
        [Route("DownloadReportDocument/{documentId:guid}")]
        public IActionResult DownloadReportDocument(Guid documentId)
        {
            var response = new ResponseDto();
            try
            {
                var document = _db.Tbl_Report_DocumentRepository
                    .FirstOrDefault(x => x.DocumentID == documentId);

                if (document == null)
                    return NotFound("Report document not found.");

                if (document.DocumentData == null ||
                    document.DocumentData.Length == 0)
                    return NotFound("Document data is unavailable.");

                //var fileName = Path.GetFileName(document.DocumentName ?? "document");

                //var contentType = GetContentType(fileName);

                //return File(
                //    document.DocumentData,
                //    contentType,
                //    fileName);
                response.Result = document;
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error downloading feedback document {DocumentId}",
                    documentId);
                response.IsSuccess = false;
                response.Message = "An error occurred while downloading the document.";

                return StatusCode(500, response);
            }
        }

        [HttpGet]
        [Route("GetFeedbackDocumentMetadata/{referenceid:long}")]
        public ActionResult<ResponseDto> GetFeedbackDocumentMetadata(
    long referenceid,
    [FromQuery] string? referenceTable)
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

                if (string.IsNullOrWhiteSpace(referenceTable))
                {
                    response.IsSuccess = false;
                    response.Message = "Reference table is required.";
                    return BadRequest(response);
                }

                if (referenceTable.Length > 128)
                {
                    response.IsSuccess = false;
                    response.Message = "Reference table name cannot exceed 128 characters.";
                    return BadRequest(response);
                }

                var documents = _db.Tbl_Feedback_DocumentRepository
                    .Where(x =>
                        x.ReferenceID == referenceid)
                    .OrderByDescending(x => x.UploadedOn)
                    .Select(x => new
                    {
                        x.DocumentID,
                        x.DocumentAutoID,
                        x.ReferenceID,
                        x.DocumentName,
                        x.DocumentType,
                        x.UploadedBy,
                        x.UploadedOn,
                        DownloadUrl = "/api/updownload/DownloadFeedbackDocument/" + x.DocumentID
                    })
                    .ToList();

                response.IsSuccess = true;
                response.Message = "Feedback document metadata retrieved successfully.";
                response.Result = documents;

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error retrieving feedback document metadata for ReferenceID {ReferenceID}",
                    referenceid);

                response.IsSuccess = false;
                response.Message = "An error occurred while retrieving feedback document metadata.";

                return StatusCode(500, response);
            }
        }

        [HttpGet]
        [Route("GetReportDocumentMetadata/{referenceid:long}")]
        public ActionResult<ResponseDto> GetReportDocumentMetadata(
            long referenceid,
            [FromQuery] string? referenceTable)
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

                if (string.IsNullOrWhiteSpace(referenceTable))
                {
                    response.IsSuccess = false;
                    response.Message = "Reference table is required.";
                    return BadRequest(response);
                }

                if (referenceTable.Length > 128)
                {
                    response.IsSuccess = false;
                    response.Message = "Reference table name cannot exceed 128 characters.";
                    return BadRequest(response);
                }

                var documents = _db.Tbl_Report_DocumentRepository
                    .Where(x =>
                        x.ReferenceID == referenceid)
                    .OrderByDescending(x => x.UploadedOn)
                    .Select(x => new
                    {
                        x.DocumentID,
                        x.DocumentAutoID,
                        x.ReferenceID,
                        x.DocumentName,
                        x.DocumentType,
                        x.UploadedBy,
                        x.UploadedOn,
                        DownloadUrl = "/api/updownload/DownloadReportDocument/" + x.DocumentID
                    })
                    .ToList();

                response.IsSuccess = true;
                response.Message = "Report document metadata retrieved successfully.";
                response.Result = documents;

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error retrieving report document metadata for ReferenceID {ReferenceID}",
                    referenceid);

                response.IsSuccess = false;
                response.Message = "An error occurred while retrieving report document metadata.";

                return StatusCode(500, response);
            }
        }

        private string GetContentType(string? fileName)
        {
            var extension = Path.GetExtension(fileName ?? "")
                .ToLowerInvariant();

            return extension switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/octet-stream"
            };
        }
    }
}
