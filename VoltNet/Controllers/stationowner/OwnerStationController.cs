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

    // GET: owner/stations/PickLocation
    [HttpGet]
    public IActionResult PickLocation()
    {
        return View("~/Views/stationowner/OwnerPickLocation.cshtml");
    }
}
