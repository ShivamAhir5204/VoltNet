using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using Microsoft.AspNetCore.Authorization;

namespace VoltNet.Controllers.admin;

[Authorize(Roles = "SuperAdmin,Admin")]
[Route("admin/Station/{action=Index}/{id?}")]
public class StationController : Controller
{
    private readonly AppDbContext _context;

    public StationController(AppDbContext context)
    {
        _context = context;
    }

    // GET: admin/Station
    public async Task<IActionResult> Index()
    {
        var stations = await _context.Stations
            .Include(s => s.OwnerUser)
            .ToListAsync();
        return View("~/Views/admin/station/Index.cshtml", stations);
    }

    // GET: admin/Station/Map
    public IActionResult Map(Guid? highlight)
    {
        ViewBag.HighlightId = highlight;
        return View("~/Views/admin/station/Map.cshtml");
    }

    // GET: admin/Station/MapData (JSON API for map)
    [HttpGet]
    public async Task<IActionResult> MapData()
    {
        var stations = await _context.Stations
            .Include(s => s.OwnerUser)
            .Where(s => s.Latitude != 0 || s.Longitude != 0)
            .Select(s => new
            {
                s.Id,
                s.Name,
                Owner = s.OwnerUser != null ? s.OwnerUser.Fullname : "—",
                s.Address,
                s.City,
                s.State,
                s.Latitude,
                s.Longitude,
                s.Status,
                Opening = s.OpeningTime.ToString(@"hh\:mm"),
                Closing = s.ClosingTime.ToString(@"hh\:mm")
            })
            .ToListAsync();
        return Json(stations);
    }

    // GET: admin/Station/PickLocation
    public IActionResult PickLocation(string returnTo, Guid? id)
    {
        ViewBag.ReturnTo = returnTo ?? "Create";
        ViewBag.StationId = id;
        return View("~/Views/admin/station/PickLocation.cshtml");
    }

    // POST: admin/Station/LoadDetails
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadDetails(Guid id)
    {
        var station = await _context.Stations
            .Include(s => s.OwnerUser)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (station == null)
            return NotFound();

        return View("~/Views/admin/station/Details.cshtml", station);
    }

    // GET: admin/Station/Create
    public async Task<IActionResult> Create()
    {
        await PopulateOwnerDropdown();
        return View("~/Views/admin/station/Create.cshtml");
    }

    // POST: admin/Station/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,OwnerUserId,Address,City,State,Latitude,Longitude,OpeningTime,ClosingTime,Status")] Station station)
    {
        if (ModelState.IsValid)
        {
            station.Id = Guid.NewGuid();
            station.CreatedAt = DateTime.UtcNow;
            _context.Add(station);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        await PopulateOwnerDropdown(station.OwnerUserId);
        return View("~/Views/admin/station/Create.cshtml", station);
    }

    // POST: admin/Station/LoadEdit (from Index table — no id in URL)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadEdit(Guid id)
    {
        var station = await _context.Stations.FindAsync(id);
        if (station == null)
            return NotFound();

        await PopulateOwnerDropdown(station.OwnerUserId);
        return View("~/Views/admin/station/Edit.cshtml", station);
    }

    // GET: admin/Station/Edit/{id} (for redirect back from PickLocation)
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var station = await _context.Stations.FindAsync(id);
        if (station == null)
            return NotFound();

        await PopulateOwnerDropdown(station.OwnerUserId);
        return View("~/Views/admin/station/Edit.cshtml", station);
    }

    // POST: admin/Station/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([Bind("Id,Name,OwnerUserId,Address,City,State,Latitude,Longitude,OpeningTime,ClosingTime,Status,CreatedAt")] Station station)
    {
        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(station);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StationExists(station.Id))
                    return NotFound();
                else
                    throw;
            }
            return RedirectToAction(nameof(Index));
        }
        await PopulateOwnerDropdown(station.OwnerUserId);
        return View("~/Views/admin/station/Edit.cshtml", station);
    }

    // POST: admin/Station/Delete
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var station = await _context.Stations.FindAsync(id);
        if (station != null)
        {
            _context.Stations.Remove(station);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private bool StationExists(Guid id)
    {
        return _context.Stations.Any(e => e.Id == id);
    }

    private async Task PopulateOwnerDropdown(Guid? selectedId = null)
    {
        var owners = await _context.UserMasters
            .Where(u => u.Role == "StationOwner")
            .OrderBy(u => u.Fullname)
            .ToListAsync();

        ViewBag.OwnerList = new SelectList(owners, "Id", "Fullname", selectedId);
    }
}
