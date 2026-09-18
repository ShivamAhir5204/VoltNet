using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace VoltNet.Controllers.stationowner;

[Authorize(Roles = "StationOwner")]
[Route("owner/stations/{action=Index}/{id?}")]
public class OwnerStationController : Controller
{
    private readonly AppDbContext _context;

    public OwnerStationController(AppDbContext context)
    {
        _context = context;
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
            .Where(s => s.OwnerUserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return View("~/Views/stationowner/Stations.cshtml", stations);
    }

    // POST: owner/stations/Details
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Details(Guid id)
    {
        var userId = GetUserId();
        var station = await _context.Stations
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

    // GET: owner/stations/Create
    [HttpGet]
    public IActionResult Create()
    {
        return View("~/Views/stationowner/CreateStation.cshtml");
    }

    // POST: owner/stations/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Station model)
    {
        if (ModelState.IsValid)
        {
            model.Id = Guid.NewGuid();
            model.OwnerUserId = GetUserId();
            model.Status = "PendingVerification";
            model.CreatedAt = DateTime.UtcNow;

            _context.Stations.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Station created successfully and is pending verification.";
            return RedirectToAction(nameof(Index));
        }
        return View("~/Views/stationowner/CreateStation.cshtml", model);
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

    // POST: owner/stations/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        var station = await _context.Stations
            .FirstOrDefaultAsync(s => s.Id == id && s.OwnerUserId == userId);

        if (station == null)
            return NotFound();

        // Check if managers are assigned
        var hasManagers = await _context.StationManagers.AnyAsync(m => m.StationId == id);
        if (hasManagers)
        {
            TempData["Error"] = "Cannot delete station because station managers are assigned to it. Please reassign or delete them first.";
            return RedirectToAction(nameof(Index));
        }

        _context.Stations.Remove(station);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Station '{station.Name}' deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    // GET: owner/stations/PickLocation
    [HttpGet]
    public IActionResult PickLocation(string? returnTo = null, Guid? id = null)
    {
        ViewBag.ReturnTo = returnTo ?? "Create";
        ViewBag.StationId = id;
        return View("~/Views/stationowner/OwnerPickLocation.cshtml");
    }
}
