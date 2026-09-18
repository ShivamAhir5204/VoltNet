using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VoltNet.Data;
using VoltNet.Models;

namespace VoltNet.Controllers.stationmanager;

[Authorize(Roles = "StationManager")]
[Route("manager/chargers")]
public class ManagerChargerController : Controller
{
    private readonly AppDbContext _context;

    public ManagerChargerController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirstValue("UserId")!);
    }

    private async Task<Guid?> GetStationIdAsync(Guid userId)
    {
        var manager = await _context.StationManagers.FirstOrDefaultAsync(m => m.UserId == userId);
        return manager?.StationId;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var stationId = await GetStationIdAsync(GetUserId());
        if (stationId == null) return RedirectToAction("Error", "Home");

        var chargers = await _context.Chargers
            .Where(c => c.StationId == stationId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return View("~/Views/stationmanager/Chargers.cshtml", chargers);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        var stationId = await GetStationIdAsync(GetUserId());
        if (stationId == null) return RedirectToAction("Error", "Home");

        return View("~/Views/stationmanager/CreateCharger.cshtml");
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Charger model)
    {
        var stationId = await GetStationIdAsync(GetUserId());
        if (stationId == null) return RedirectToAction("Error", "Home");

        if (ModelState.IsValid)
        {
            model.Id = Guid.NewGuid();
            model.StationId = stationId.Value;
            model.CreatedAt = DateTime.UtcNow;

            _context.Chargers.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Charger added successfully.";
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/stationmanager/CreateCharger.cshtml", model);
    }

    [HttpGet("edit/{id}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var stationId = await GetStationIdAsync(GetUserId());
        if (stationId == null) return RedirectToAction("Error", "Home");

        var charger = await _context.Chargers
            .FirstOrDefaultAsync(c => c.Id == id && c.StationId == stationId); // Security constraint

        if (charger == null) return NotFound();

        return View("~/Views/stationmanager/EditCharger.cshtml", charger);
    }

    [HttpPost("edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Charger model)
    {
        var stationId = await GetStationIdAsync(GetUserId());
        if (stationId == null) return RedirectToAction("Error", "Home");

        if (id != model.Id) return BadRequest();

        if (ModelState.IsValid)
        {
            var charger = await _context.Chargers
                .FirstOrDefaultAsync(c => c.Id == id && c.StationId == stationId); // Security constraint

            if (charger == null) return NotFound();

            charger.Name = model.Name;
            charger.ConnectorType = model.ConnectorType;
            charger.CapacityKw = model.CapacityKw;
            charger.Status = model.Status;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Charger updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/stationmanager/EditCharger.cshtml", model);
    }

    [HttpPost("delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var stationId = await GetStationIdAsync(GetUserId());
        if (stationId == null) return RedirectToAction("Error", "Home");

        var charger = await _context.Chargers
            .FirstOrDefaultAsync(c => c.Id == id && c.StationId == stationId); // Security constraint

        if (charger != null)
        {
            _context.Chargers.Remove(charger);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Charger deleted successfully.";
        }
        else
        {
            TempData["Error"] = "Charger not found.";
        }

        return RedirectToAction(nameof(Index));
    }
}
