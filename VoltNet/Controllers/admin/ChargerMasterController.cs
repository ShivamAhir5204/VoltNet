using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using Microsoft.AspNetCore.Authorization;

namespace VoltNet.Controllers.admin;

[Authorize(Roles = "SuperAdmin")]
[Route("admin/ChargerMaster/{action=Index}/{id?}")]
public class ChargerMasterController : Controller
{
    private readonly AppDbContext _context;

    public ChargerMasterController(AppDbContext context)
    {
        _context = context;
    }

    // GET: admin/ChargerMaster
    public async Task<IActionResult> Index()
    {
        var chargers = await _context.Chargers
            .Include(c => c.Station)
            .ToListAsync();
        return View("~/Views/admin/chargermaster/Index.cshtml", chargers);
    }

    // POST: admin/ChargerMaster/LoadDetails
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadDetails(int id)
    {
        var charger = await _context.Chargers
            .Include(c => c.Station)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (charger == null)
            return NotFound();

        return View("~/Views/admin/chargermaster/Details.cshtml", charger);
    }

    // GET: admin/ChargerMaster/Create
    public async Task<IActionResult> Create()
    {
        await PopulateStationsDropdown();
        return View("~/Views/admin/chargermaster/Create.cshtml");
    }

    // POST: admin/ChargerMaster/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("StationId,ChargerCode,Type,ConnectorType,PowerRatingKw,RatePerKwh,Status")] Charger charger)
    {
        if (ModelState.IsValid)
        {
            _context.Add(charger);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        await PopulateStationsDropdown(charger.StationId);
        return View("~/Views/admin/chargermaster/Create.cshtml", charger);
    }

    // POST: admin/ChargerMaster/LoadEdit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadEdit(int id)
    {
        var charger = await _context.Chargers.FindAsync(id);
        if (charger == null)
            return NotFound();

        await PopulateStationsDropdown(charger.StationId);
        return View("~/Views/admin/chargermaster/Edit.cshtml", charger);
    }

    // POST: admin/ChargerMaster/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,StationId,ChargerCode,Type,ConnectorType,PowerRatingKw,RatePerKwh,Status,LastServicedAt")] Charger charger)
    {
        if (id != charger.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(charger);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ChargerExists(charger.Id))
                    return NotFound();
                else
                    throw;
            }
            return RedirectToAction(nameof(Index));
        }
        await PopulateStationsDropdown(charger.StationId);
        return View("~/Views/admin/chargermaster/Edit.cshtml", charger);
    }

    // POST: admin/ChargerMaster/Delete
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var charger = await _context.Chargers.FindAsync(id);
        if (charger != null)
        {
            _context.Chargers.Remove(charger);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private bool ChargerExists(int id)
    {
        return _context.Chargers.Any(e => e.Id == id);
    }

    private async Task PopulateStationsDropdown(int? selectedId = null)
    {
        var stations = await _context.Stations
            .Where(s => s.Status == "Active")
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.Name + " (" + s.City + ")",
                Selected = selectedId.HasValue && s.Id == selectedId.Value
            })
            .ToListAsync();

        ViewBag.Stations = stations;
    }
}
