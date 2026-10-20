using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using VoltNet.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace VoltNet.Controllers;

[Authorize(Roles = "Customer")]
public class PartnerController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public PartnerController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirstValue("UserId")!);
    }

    [HttpGet("partner/apply")]
    public async Task<IActionResult> Apply()
    {
        var userId = GetUserId();

        // Check if already applied
        var existing = await _context.StationOwners
            .Include(o => o.Documents)
            .FirstOrDefaultAsync(o => o.UserId == userId);

        if (existing != null)
        {
            return RedirectToAction(nameof(Status));
        }

        var user = await _context.UserMasters.FindAsync(userId);
        ViewBag.UserFullName = user?.Fullname ?? "";
        ViewBag.UserEmail = user?.Email ?? "";
        ViewBag.UserPhone = user?.Mobile ?? "";

        return View("~/Views/Partner/Apply.cshtml");
    }

    [HttpPost("partner/apply")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(
        string fullName, string? businessName, string phone,
        string? businessRegistrationNumber, string? address,
        string? city, string? state, string? gstNumber,
        IFormFile? panCardFile, IFormFile? idProofFile,
        IFormFile? businessDocFile, IFormFile? gstDocFile,
        IFormFile? bankProofFile)
    {
        var userId = GetUserId();

        // Check if already applied
        var existing = await _context.StationOwners.FirstOrDefaultAsync(o => o.UserId == userId);
        if (existing != null)
        {
            return RedirectToAction(nameof(Status));
        }

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(phone))
        {
            ViewBag.Error = "Full Name and Phone are required.";
            return View("~/Views/Partner/Apply.cshtml");
        }

        // Validate uploaded files if provided
        var filesToValidate = new[]
        {
            (panCardFile, "PAN Card"),
            (idProofFile, "ID / Address Proof"),
            (businessDocFile, "Business Registration"),
            (gstDocFile, "GST Certificate"),
            (bankProofFile, "Bank Proof / Cancelled Cheque")
        };

        foreach (var (file, label) in filesToValidate)
        {
            if (file != null && file.Length > 0)
            {
                if (!FileValidationHelper.IsValidDocument(file, out var errorMsg))
                {
                    ViewBag.Error = $"{label}: {errorMsg}";
                    return View("~/Views/Partner/Apply.cshtml");
                }
            }
        }

        var owner = new StationOwner
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = fullName.Trim(),
            BusinessName = businessName?.Trim(),
            Phone = phone.Trim(),
            BusinessRegistrationNumber = businessRegistrationNumber?.Trim(),
            Address = address?.Trim(),
            City = city?.Trim(),
            State = state?.Trim(),
            GSTNumber = gstNumber?.Trim(),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.StationOwners.Add(owner);

        // Save documents
        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "owner-kyc");
        if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

        var docMappings = new[]
        {
            (panCardFile, "PAN Card", "Identity / PAN"),
            (idProofFile, "Aadhaar / ID Proof", "Address / ID Proof"),
            (businessDocFile, "Business Registration / MSME", businessRegistrationNumber),
            (gstDocFile, "GST Certificate", gstNumber),
            (bankProofFile, "Bank Cancelled Cheque", "Bank Account Proof")
        };

        foreach (var (file, docType, docNumber) in docMappings)
        {
            if (file != null && file.Length > 0)
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var safeFileName = $"kyc_{owner.Id}_{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadsDir, safeFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var ownerDoc = new StationOwnerDocument
                {
                    Id = Guid.NewGuid(),
                    StationOwnerId = owner.Id,
                    DocumentType = docType,
                    DocumentNumber = docNumber?.Trim(),
                    FilePath = $"/uploads/owner-kyc/{safeFileName}",
                    OriginalFileName = Path.GetFileName(file.FileName),
                    FileExtension = ext,
                    FileSizeBytes = file.Length,
                    Status = "Pending",
                    UploadedAt = DateTime.UtcNow
                };

                _context.StationOwnerDocuments.Add(ownerDoc);
            }
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = "Your partner application and verification documents have been submitted successfully! Our team will review them shortly.";
        return RedirectToAction(nameof(Status));
    }

    [HttpPost("partner/resubmit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resubmit(
        string? phone, string? businessName, string? gstNumber,
        IFormFile? panCardFile, IFormFile? idProofFile,
        IFormFile? businessDocFile, IFormFile? gstDocFile,
        IFormFile? bankProofFile)
    {
        var userId = GetUserId();
        var owner = await _context.StationOwners
            .Include(o => o.Documents)
            .FirstOrDefaultAsync(o => o.UserId == userId);

        if (owner == null) return RedirectToAction(nameof(Apply));

        if (owner.Status != "Rejected")
        {
            TempData["Info"] = "Only rejected applications can be resubmitted.";
            return RedirectToAction(nameof(Status));
        }

        // Validate files
        var filesToValidate = new[]
        {
            (panCardFile, "PAN Card"),
            (idProofFile, "ID / Address Proof"),
            (businessDocFile, "Business Registration"),
            (gstDocFile, "GST Certificate"),
            (bankProofFile, "Bank Proof / Cancelled Cheque")
        };

        foreach (var (file, label) in filesToValidate)
        {
            if (file != null && file.Length > 0)
            {
                if (!FileValidationHelper.IsValidDocument(file, out var errorMsg))
                {
                    TempData["Error"] = $"{label}: {errorMsg}";
                    return RedirectToAction(nameof(Status));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(phone)) owner.Phone = phone.Trim();
        if (!string.IsNullOrWhiteSpace(businessName)) owner.BusinessName = businessName.Trim();
        if (!string.IsNullOrWhiteSpace(gstNumber)) owner.GSTNumber = gstNumber.Trim();

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "owner-kyc");
        if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

        var docMappings = new[]
        {
            (panCardFile, "PAN Card", "Identity / PAN"),
            (idProofFile, "Aadhaar / ID Proof", "Address / ID Proof"),
            (businessDocFile, "Business Registration / MSME", owner.BusinessRegistrationNumber),
            (gstDocFile, "GST Certificate", gstNumber ?? owner.GSTNumber),
            (bankProofFile, "Bank Cancelled Cheque", "Bank Account Proof")
        };

        foreach (var (file, docType, docNumber) in docMappings)
        {
            if (file != null && file.Length > 0)
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var safeFileName = $"kyc_{owner.Id}_{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadsDir, safeFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Check if existing document of this type exists
                var existingDoc = owner.Documents.FirstOrDefault(d => d.DocumentType == docType);
                if (existingDoc != null)
                {
                    existingDoc.FilePath = $"/uploads/owner-kyc/{safeFileName}";
                    existingDoc.OriginalFileName = Path.GetFileName(file.FileName);
                    existingDoc.FileExtension = ext;
                    existingDoc.FileSizeBytes = file.Length;
                    existingDoc.Status = "Pending";
                    existingDoc.RejectionReason = null;
                    existingDoc.UploadedAt = DateTime.UtcNow;
                }
                else
                {
                    var newDoc = new StationOwnerDocument
                    {
                        Id = Guid.NewGuid(),
                        StationOwnerId = owner.Id,
                        DocumentType = docType,
                        DocumentNumber = docNumber?.Trim(),
                        FilePath = $"/uploads/owner-kyc/{safeFileName}",
                        OriginalFileName = Path.GetFileName(file.FileName),
                        FileExtension = ext,
                        FileSizeBytes = file.Length,
                        Status = "Pending",
                        UploadedAt = DateTime.UtcNow
                    };
                    _context.StationOwnerDocuments.Add(newDoc);
                }
            }
        }

        // Reset all rejected documents back to Pending
        foreach (var doc in owner.Documents.Where(d => d.Status == "Rejected"))
        {
            doc.Status = "Pending";
        }

        owner.Status = "Pending";
        owner.RejectionReason = null;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Your application and documents have been resubmitted for admin verification.";
        return RedirectToAction(nameof(Status));
    }

    [HttpGet("partner/status")]
    public async Task<IActionResult> Status()
    {
        var userId = GetUserId();

        var owner = await _context.StationOwners
            .Include(o => o.Documents)
            .FirstOrDefaultAsync(o => o.UserId == userId);

        if (owner == null)
        {
            return RedirectToAction(nameof(Apply));
        }

        return View("~/Views/Partner/Status.cshtml", owner);
    }
}

