using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;

namespace VoltNet.Services;

public static class FileValidationHelper
{
    private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public static bool IsValidDocument(IFormFile? file, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (file == null || file.Length == 0)
        {
            errorMessage = "Please select a document file to upload.";
            return false;
        }

        if (file.Length > MaxFileSizeBytes)
        {
            errorMessage = $"File size ({file.Length / (1024 * 1024):F1} MB) exceeds the maximum allowed limit of 10 MB.";
            return false;
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            errorMessage = $"File extension '{ext}' is not permitted. Only PDF, JPG, JPEG, PNG, and WEBP documents are allowed.";
            return false;
        }

        // Validate Magic Bytes
        try
        {
            using var stream = file.OpenReadStream();
            byte[] header = new byte[12];
            int bytesRead = stream.Read(header, 0, header.Length);

            if (bytesRead < 4)
            {
                errorMessage = "Uploaded file appears to be corrupted or empty.";
                return false;
            }

            // PDF: %PDF (0x25, 0x50, 0x44, 0x46)
            if (ext == ".pdf")
            {
                if (header[0] != 0x25 || header[1] != 0x50 || header[2] != 0x44 || header[3] != 0x46)
                {
                    errorMessage = "The file content does not match a valid PDF document.";
                    return false;
                }
            }
            // JPEG: FF D8 FF
            else if (ext == ".jpg" || ext == ".jpeg")
            {
                if (header[0] != 0xFF || header[1] != 0xD8 || header[2] != 0xFF)
                {
                    errorMessage = "The file content does not match a valid JPEG image.";
                    return false;
                }
            }
            // PNG: 89 50 4E 47
            else if (ext == ".png")
            {
                if (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47)
                {
                    errorMessage = "The file content does not match a valid PNG image.";
                    return false;
                }
            }
            // WEBP: RIFF at 0..3, WEBP at 8..11
            else if (ext == ".webp")
            {
                if (bytesRead >= 12 &&
                    header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                    header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
                {
                    // Valid WEBP
                }
                else
                {
                    errorMessage = "The file content does not match a valid WEBP image.";
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = "Unable to inspect file headers: " + ex.Message;
            return false;
        }

        return true;
    }
}
