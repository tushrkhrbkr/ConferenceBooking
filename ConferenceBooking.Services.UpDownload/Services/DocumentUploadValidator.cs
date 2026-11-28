
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
            long? referenceId)
        {
            if (!referenceId.HasValue || referenceId.Value <= 0)
                return (false, "A valid ReferenceID is required.");

            if (string.IsNullOrWhiteSpace(fileName))
                return (false, "Document name is required.");

            if (fileName.Length > 260)
                return (false, "Document name cannot exceed 260 characters.");

            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                fileName.Contains('/') ||
                fileName.Contains('\\'))
            {
                return (false, "Document name contains invalid characters.");
            }

            if (fileData == null || fileData.Length == 0)
                return (false, "Uploaded document is empty.");

            if (fileData.Length > _maxFileSizeBytes)
                return (false, "Document exceeds the maximum permitted file size.");

            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            if (!_allowedExtensions.Contains(extension))
                return (false, "This document file type is not permitted.");

            if (!HasValidFileSignature(extension, fileData))
                return (false, "The uploaded file content does not match its file extension.");

            return (true, "Document validation successful.");
        }

        private static bool HasValidFileSignature(
            string extension,
            byte[] data)
        {
            if (data.Length < 4)
                return false;

            return extension switch
            {
                // PDF: %PDF
                ".pdf" =>
                    StartsWith(data, 0x25, 0x50, 0x44, 0x46),

                // Legacy Microsoft Word and Excel: OLE Compound File
                ".doc" or ".xls" =>
                    StartsWith(data, 0xD0, 0xCF, 0x11, 0xE0,
                                    0xA1, 0xB1, 0x1A, 0xE1),

                // DOCX and XLSX are ZIP-based Office formats
                ".docx" or ".xlsx" =>
                    StartsWith(data, 0x50, 0x4B, 0x03, 0x04) ||
                    StartsWith(data, 0x50, 0x4B, 0x05, 0x06) ||
                    StartsWith(data, 0x50, 0x4B, 0x07, 0x08),

                // JPEG
                ".jpg" or ".jpeg" =>
                    StartsWith(data, 0xFF, 0xD8, 0xFF),

                // PNG
                ".png" =>
                    StartsWith(data, 0x89, 0x50, 0x4E, 0x47,
                                    0x0D, 0x0A, 0x1A, 0x0A),

                _ => false
            };
        }

        private static bool StartsWith(
            byte[] data,
            params byte[] signature)
        {
            if (data.Length < signature.Length)
                return false;

            for (int i = 0; i < signature.Length; i++)
            {
                if (data[i] != signature[i])
                    return false;
            }

            return true;
        }
    }
}
