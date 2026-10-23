using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using VoltNet.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace VoltNet.Controllers.stationowner;

[Authorize(Roles = "StationOwner")]
[Route("owner/stations/{action=Index}/{id?}")]
public class OwnerStationController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public OwnerStationController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirstValue("UserId")!);
    }

    // GET: owner/stations
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();
        var stations = await _context.Stations
            .Include(s => s.Documents)
            .Where(s => s.OwnerUserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return View("~/Views/stationowner/Stations.cshtml", stations);
    }

    // GET & POST: owner/stations/Details/{id}
    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> Details(Guid id)
    {
        var userId = GetUserId();
        var station = await _context.Stations
            .Include(s => s.Documents)
            .FirstOrDefaultAsync(s => s.Id == id && s.OwnerUserId == userId);

        if (station == null)
            return NotFound();

        // Get managers assigned to this station
        var managers = await _context.StationManagers
            .Include(m => m.User)
            .Where(m => m.StationId == id)
            .ToListAsync();

        ViewBag.Managers = managers;

        return View("~/Views/stationowner/StationDetails.cshtml", station);
    }

    // GET: owner/stations/PickLocation
    [HttpGet]
    public IActionResult PickLocation(string? returnTo, Guid? id)
    {
        ViewBag.ReturnTo = returnTo ?? "Create";
        ViewBag.StationId = id;
        return View("~/Views/stationowner/OwnerPickLocation.cshtml");
    }

    // GET: owner/stations/Create
    [HttpGet]
    public IActionResult Create()
    {
        return View("~/Views/stationowner/CreateStation.cshtml");
    }

    // POST: owner/stations/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        Station model,
        IFormFile? discomLetterFile,
        IFormFile? landLeaseFile,
        IFormFile? fireNocFile,
        IFormFile? sitePhotosFile)
    {
        // Enforce mandatory requirement for all 4 statutory documents
        if (discomLetterFile == null || discomLetterFile.Length == 0)
        {
            ModelState.AddModelError("discomLetterFile", "DISCOM Electricity Sanction Letter is mandatory for station verification.");
        }
        if (landLeaseFile == null || landLeaseFile.Length == 0)
        {
            ModelState.AddModelError("landLeaseFile", "Land Lease Agreement / Ownership Deed is mandatory for station verification.");
        }
        if (fireNocFile == null || fireNocFile.Length == 0)
        {
            ModelState.AddModelError("fireNocFile", "Fire Safety NOC / Clearance Certificate is mandatory for station verification.");
        }
        if (sitePhotosFile == null || sitePhotosFile.Length == 0)
        {
            ModelState.AddModelError("sitePhotosFile", "Station Site & Installation Photographs are mandatory for station verification.");
        }

        // Validate uploaded files
        var filesToValidate = new[]
        {
            (discomLetterFile, "DISCOM Sanction Letter"),
            (landLeaseFile, "Land Lease / Ownership"),
            (fireNocFile, "Fire Safety NOC"),
            (sitePhotosFile, "Station Site Photos")
        };

        foreach (var (file, label) in filesToValidate)
        {
            if (file != null && file.Length > 0)
            {
                if (!FileValidationHelper.IsValidDocument(file, out var errorMsg))
                {
                    ModelState.AddModelError("", $"{label}: {errorMsg}");
                }
            }
        }

        // Real-world validation: Coordinates
        if (model.Latitude < -90 || model.Latitude > 90 || model.Longitude < -180 || model.Longitude > 180 || (model.Latitude == 0 && model.Longitude == 0))
        {
            ModelState.AddModelError("Latitude", "Please provide valid GPS coordinates (Latitude -90 to 90, Longitude -180 to 180).");
        }

        // Real-world validation: Station duplicate name in same city for same owner
        var currentUserId = GetUserId();
        var isDuplicate = await _context.Stations
            .AnyAsync(s => s.OwnerUserId == currentUserId && s.City.ToLower() == (model.City ?? "").Trim().ToLower() && s.Name.ToLower() == (model.Name ?? "").Trim().ToLower());
        if (isDuplicate)
        {
            ModelState.AddModelError("Name", $"You already have a station named '{model.Name}' in {model.City}.");
        }

        if (ModelState.IsValid)
        {
            model.Id = Guid.NewGuid();
            model.OwnerUserId = GetUserId();
            model.Status = "PendingVerification";
            model.CreatedAt = DateTime.UtcNow;

            // Auto-assign to owner's active subscription if capacity is available
            var activeSub = await _context.OwnerSubscriptions
                .Include(os => os.Plan)
                .Include(os => os.Stations)
                .FirstOrDefaultAsync(os => os.Owner!.UserId == model.OwnerUserId && os.Status == "Active" && os.EndDate >= DateTime.UtcNow);

            if (activeSub != null && (activeSub.Plan == null || activeSub.Stations.Count < activeSub.Plan.MaxStations))
            {
                model.OwnerSubscriptionId = activeSub.Id;
            }

            _context.Stations.Add(model);

            // Handle documents
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "station-documents");
            if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

            var docMappings = new[]
            {
                (discomLetterFile, "DISCOM Sanction Letter", "Power Load Sanction"),
                (landLeaseFile, "Land Lease / Ownership", "Premises Lease"),
                (fireNocFile, "Fire Safety NOC", "Fire Safety"),
                (sitePhotosFile, "Station Site Photographs", "Site Visual Proof")
            };

            foreach (var (file, docType, docNumber) in docMappings)
            {
                if (file != null && file.Length > 0)
                {
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    var safeFileName = $"stn_doc_{model.Id}_{Guid.NewGuid()}{ext}";
                    var filePath = Path.Combine(uploadsDir, safeFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var doc = new StationDocument
                    {
                        Id = Guid.NewGuid(),
                        StationId = model.Id,
                        DocumentType = docType,
                        DocumentNumber = docNumber,
                        FilePath = $"/uploads/station-documents/{safeFileName}",
                        OriginalFileName = Path.GetFileName(file.FileName),
                        FileExtension = ext,
                        FileSizeBytes = file.Length,
                        Status = "Pending",
                        UploadedAt = DateTime.UtcNow
                    };

                    _context.StationDocuments.Add(doc);
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Station created successfully with compliance documents and is queued for admin verification.";
            return RedirectToAction(nameof(Index));
        }
        return View("~/Views/stationowner/CreateStation.cshtml", model);
    }

    // POST: owner/stations/UploadDocument
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDocument(
        Guid stationId, string documentType, string? documentNumber, IFormFile documentFile)
    {
        var userId = GetUserId();
        var station = await _context.Stations
            .FirstOrDefaultAsync(s => s.Id == stationId && s.OwnerUserId == userId);

        if (station == null) return NotFound();

        if (string.IsNullOrWhiteSpace(documentType))
        {
            TempData["Error"] = "Please select a document type.";
            return RedirectToAction(nameof(Details), new { id = stationId });
        }

        if (!FileValidationHelper.IsValidDocument(documentFile, out var errorMsg))
        {
            TempData["Error"] = errorMsg;
            return RedirectToAction(nameof(Details), new { id = stationId });
        }

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "station-documents");
        if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(documentFile.FileName).ToLowerInvariant();
        var safeFileName = $"stn_doc_{station.Id}_{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsDir, safeFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await documentFile.CopyToAsync(stream);
        }

        var doc = new StationDocument
        {
            Id = Guid.NewGuid(),
            StationId = station.Id,
            DocumentType = documentType.Trim(),
            DocumentNumber = documentNumber?.Trim(),
            FilePath = $"/uploads/station-documents/{safeFileName}",
            OriginalFileName = Path.GetFileName(documentFile.FileName),
            FileExtension = ext,
            FileSizeBytes = documentFile.Length,
            Status = "Pending",
            UploadedAt = DateTime.UtcNow
        };

        _context.StationDocuments.Add(doc);

        // If station was rejected, moving it back to PendingVerification
        if (station.Status == "Rejected")
        {
            station.Status = "PendingVerification";
            station.RejectionReason = null;
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Compliance document '{doc.DocumentType}' uploaded successfully and queued for review.";
        return RedirectToAction(nameof(Details), new { id = stationId });
    }

    // POST: owner/stations/DeleteDocument
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDocument(Guid documentId, Guid stationId)
    {
        var userId = GetUserId();
        var station = await _context.Stations
            .FirstOrDefaultAsync(s => s.Id == stationId && s.OwnerUserId == userId);

        if (station == null) return NotFound();

        var doc = await _context.StationDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.StationId == stationId);

        if (doc != null)
        {
            // Delete physical file if exists
            try
            {
                var fullPath = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
            }
            catch { }

            _context.StationDocuments.Remove(doc);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Document '{doc.DocumentType}' deleted.";
        }

        return RedirectToAction(nameof(Details), new { id = stationId });
    }

    // GET: owner/stations/Edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var userId = GetUserId();
        var station = await _context.Stations
            .FirstOrDefaultAsync(s => s.Id == id && s.OwnerUserId == userId);

        if (station == null)
            return NotFound();

        return View("~/Views/stationowner/EditStation.cshtml", station);
    }

    // POST: owner/stations/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Station model)
    {
        var userId = GetUserId();
        var existingStation = await _context.Stations
            .FirstOrDefaultAsync(s => s.Id == model.Id && s.OwnerUserId == userId);

        if (existingStation == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            existingStation.Name = model.Name;
            existingStation.Address = model.Address;
            existingStation.City = model.City;
            existingStation.State = model.State;
            existingStation.Latitude = model.Latitude;
            existingStation.Longitude = model.Longitude;
            existingStation.OpeningTime = model.OpeningTime;
            existingStation.ClosingTime = model.ClosingTime;

            // If station was rejected or suspended, editing it can set it back to PendingVerification
            if (existingStation.Status == "Rejected")
            {
                existingStation.Status = "PendingVerification";
                existingStation.RejectionReason = null;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Station updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/stationowner/EditStation.cshtml", model);
    }

    // POST: owner/stations/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        var station = await _context.Stations
            .FirstOrDefaultAsync(s => s.Id == id && s.OwnerUserId == userId);

        if (station == null)
            return NotFound();

        // Check if managers are assigned to this station
        var hasManagers = await _context.StationManagers
            .AnyAsync(m => m.StationId == id);

        if (hasManagers)
        {
            TempData["Error"] = "Cannot delete station because station managers are assigned to it. Please reassign or remove managers first.";
            return RedirectToAction(nameof(Index));
        }

        // Check if station has active bookings
        var hasActiveBookings = await _context.Bookings
            .AnyAsync(b => b.StationId == id && (b.Status == "Confirmed" || b.Status == "Charging"));

        if (hasActiveBookings)
        {
            TempData["Error"] = "Cannot delete station with active or scheduled charging bookings.";
            return RedirectToAction(nameof(Index));
        }

        // Check if historical bookings exist
        var hasHistoricalBookings = await _context.Bookings.AnyAsync(b => b.StationId == id);
        if (hasHistoricalBookings)
        {
            station.Status = "Suspended";
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Station '{station.Name}' has past charging records and cannot be permanently deleted. It has been decommissioned and archived.";
            return RedirectToAction(nameof(Index));
        }

        _context.Stations.Remove(station);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Station deleted successfully.";
        return RedirectToAction(nameof(Index));
    }
}
