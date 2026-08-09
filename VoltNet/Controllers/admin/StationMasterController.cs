using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using Microsoft.AspNetCore.Authorization;

namespace VoltNet.Controllers.admin;

[Authorize(Roles = "SuperAdmin")]
[Route("admin/StationMaster/{action=Index}/{id?}")]
public class StationMasterController : Controller
{
    private readonly AppDbContext _context;

    public StationMasterController(AppDbContext context)
    {
        _context = context;
    }

    // GET: admin/StationMaster
    public async Task<IActionResult> Index()
    {
        var stations = await _context.Stations
            .Include(s => s.Franchise)
            .ToListAsync();
        return View("~/Views/admin/stationmaster/Index.cshtml", stations);
    }

    // POST: admin/StationMaster/LoadDetails
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadDetails(int id)
    {
        var station = await _context.Stations
            .Include(s => s.Franchise)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (station == null)
            return NotFound();

        return View("~/Views/admin/stationmaster/Details.cshtml", station);
    }

    // GET: admin/StationMaster/Create
    public async Task<IActionResult> Create()
    {
        await PopulateFranchisesDropdown();
        return View("~/Views/admin/stationmaster/Create.cshtml");
    }

    // POST: admin/StationMaster/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,FranchiseId,Address,City,State,Latitude,Longitude,OpeningTime,ClosingTime,Status")] Station station)
    {
        if (ModelState.IsValid)
        {
            station.CreatedAt = DateTime.UtcNow;
            _context.Add(station);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        await PopulateFranchisesDropdown(station.FranchiseId);
        return View("~/Views/admin/stationmaster/Create.cshtml", station);
    }

    // POST: admin/StationMaster/LoadEdit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadEdit(int id)
    {
        var station = await _context.Stations.FindAsync(id);
        if (station == null)
            return NotFound();

        await PopulateFranchisesDropdown(station.FranchiseId);
        return View("~/Views/admin/stationmaster/Edit.cshtml", station);
    }

    // POST: admin/StationMaster/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,FranchiseId,Address,City,State,Latitude,Longitude,OpeningTime,ClosingTime,Status,CreatedAt")] Station station)
    {
        if (id != station.Id)
        {
            return NotFound();
        }

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
        await PopulateFranchisesDropdown(station.FranchiseId);
        return View("~/Views/admin/stationmaster/Edit.cshtml", station);
    }

    // POST: admin/StationMaster/Delete
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var station = await _context.Stations.FindAsync(id);
        if (station != null)
        {
            _context.Stations.Remove(station);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private bool StationExists(int id)
    {
        return _context.Stations.Any(e => e.Id == id);
    }

    private async Task PopulateFranchisesDropdown(Guid? selectedId = null)
    {
        var franchises = await _context.FranchiseMasters
            .OrderBy(f => f.FranchiseName)
            .Select(f => new SelectListItem
            {
                Value = f.Id.ToString(),
                Text = f.FranchiseName,
                Selected = selectedId.HasValue && f.Id == selectedId.Value
            })
            .ToListAsync();

        ViewBag.Franchises = franchises;
    }
}
