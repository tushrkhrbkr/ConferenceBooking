using Microsoft.Extensions.Configuration;

namespace ConferenceBooking.Services.UpDownload.Services
{
    public class DocumentUploadValidator
    {
        private readonly long _maxFileSizeBytes;
        private readonly HashSet<string> _allowedExtensions;

        public DocumentUploadValidator(IConfiguration configuration)
        {
            var section = configuration.GetSection("DocumentUpload");

            var maxSizeMB = section.GetValue<int>("MaxFileSizeMB", 20);

            _maxFileSizeBytes = (long)maxSizeMB * 1024 * 1024;

            _allowedExtensions = section
                .GetSection("AllowedExtensions")
                .Get<string[]>()
                ?.Select(x => x.Trim().ToLowerInvariant())
                .ToHashSet()
                ?? new HashSet<string>();
        }

        public (bool IsValid, string Message) Validate(
            string? fileName,
            byte[]? fileData,
            string? referenceTable,
            long? referenceId)
        {
            if (string.IsNullOrWhiteSpace(referenceTable))
            {
                return (false, "Reference table is required.");
            }

            if (referenceTable.Length > 128)
            {
                return (false, "Reference table cannot exceed 128 characters.");
            }

            if (!referenceId.HasValue || referenceId.Value <= 0)
            {
                return (false, "A valid ReferenceID is required.");
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return (false, "Document name is required.");
            }

            if (fileName.Length > 260)
            {
                return (false, "Document name cannot exceed 260 characters.");
            }

            if (fileData == null || fileData.Length == 0)
            {
                return (false, "Uploaded document is empty.");
            }

            if (fileData.Length > _maxFileSizeBytes)
            {
                return (false, "Document exceeds the maximum permitted file size.");
            }

            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            if (!_allowedExtensions.Contains(extension))
            {
                return (false, "This document file type is not permitted.");
            }

            return (true, "Document validation successful.");
        }
    }
}