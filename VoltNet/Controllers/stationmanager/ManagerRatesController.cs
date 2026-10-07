using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VoltNet.Data;
using VoltNet.Models;

namespace VoltNet.Controllers.stationmanager;

[Authorize(Roles = "StationManager")]
[Route("manager/rates")]
public class ManagerRatesController : Controller
{
    private readonly AppDbContext _context;

    public ManagerRatesController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue("UserId")!);

    private async Task<StationManager?> GetCurrentManagerAsync()
    {
        var userId = GetUserId();
        return await _context.StationManagers
            .Include(m => m.Station)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.IsActive);
    }

    // GET: /manager/rates
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var manager = await GetCurrentManagerAsync();
        if (manager == null || manager.Station == null)
            return RedirectToAction("Error", "Home");

        var station = await _context.Stations
            .Include(s => s.Chargers)
            .Include(s => s.ChargingRates)
            .FirstOrDefaultAsync(s => s.Id == manager.StationId);

        if (station == null) return NotFound();

        var availableConnectors = station.Chargers
            .Select(c => c.ConnectorType)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        ViewBag.Station = station;
        ViewBag.CanManageRates = manager.CanManageRates;
        ViewBag.AvailableConnectors = availableConnectors;

        var rates = station.ChargingRates
            .OrderByDescending(r => r.IsActive)
            .ThenBy(r => r.ConnectorType)
            .ToList();

        return View("~/Views/stationmanager/Rates.cshtml", rates);
    }

    // POST: /manager/rates/create
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string connectorType, decimal ratePerKwh, decimal? ratePerHour)
    {
        var manager = await GetCurrentManagerAsync();
        if (manager == null || !manager.CanManageRates)
        {
            TempData["Error"] = "Permission Denied: Station Owner has not granted you permission to configure charging rates.";
            return RedirectToAction(nameof(Index));
        }

        var station = await _context.Stations
            .Include(s => s.Chargers)
            .FirstOrDefaultAsync(s => s.Id == manager.StationId);

        if (station == null) return NotFound();

        if (string.IsNullOrWhiteSpace(connectorType))
        {
            TempData["Error"] = "Please select a valid connector type.";
            return RedirectToAction(nameof(Index));
        }

        var trimmedConnector = connectorType.Trim();

        if (!station.Chargers.Any(c => c.ConnectorType.Equals(trimmedConnector, StringComparison.OrdinalIgnoreCase)))
        {
            TempData["Error"] = $"Cannot add rate: Station does not have '{trimmedConnector}' chargers installed.";
            return RedirectToAction(nameof(Index));
        }

        if (ratePerKwh <= 0 || ratePerKwh > 150)
        {
            TempData["Error"] = "Rate per kWh must be greater than ₹0.00 and no more than ₹150.00.";
            return RedirectToAction(nameof(Index));
        }

        if (ratePerHour.HasValue && (ratePerHour.Value < 0 || ratePerHour.Value > 2000))
        {
            TempData["Error"] = "Hourly rate must be between ₹0.00 and ₹2000.00.";
            return RedirectToAction(nameof(Index));
        }

        var existingActive = await _context.ChargingRates
            .FirstOrDefaultAsync(r => r.StationId == manager.StationId && r.ConnectorType == trimmedConnector && r.IsActive);

        if (existingActive != null)
        {
            TempData["Error"] = $"An active rate for '{trimmedConnector}' already exists. Please edit or deactivate the existing rate first.";
            return RedirectToAction(nameof(Index));
        }

        var newRate = new ChargingRate
        {
            Id = Guid.NewGuid(),
            StationId = manager.StationId,
            ConnectorType = trimmedConnector,
            RatePerKwh = Math.Round(ratePerKwh, 2),
            RatePerHour = ratePerHour.HasValue ? Math.Round(ratePerHour.Value, 2) : null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.ChargingRates.Add(newRate);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Charging rate for {trimmedConnector} created successfully.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /manager/rates/edit
    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, decimal ratePerKwh, decimal? ratePerHour)
    {
        var manager = await GetCurrentManagerAsync();
        if (manager == null || !manager.CanManageRates)
        {
            TempData["Error"] = "Permission Denied: Station Owner has not granted you permission to edit charging rates.";
            return RedirectToAction(nameof(Index));
        }

        var rate = await _context.ChargingRates
            .FirstOrDefaultAsync(r => r.Id == id && r.StationId == manager.StationId);

        if (rate == null) return NotFound();

        if (ratePerKwh <= 0 || ratePerKwh > 150)
        {
            TempData["Error"] = "Rate per kWh must be greater than ₹0.00 and no more than ₹150.00.";
            return RedirectToAction(nameof(Index));
        }

        if (ratePerHour.HasValue && (ratePerHour.Value < 0 || ratePerHour.Value > 2000))
        {
            TempData["Error"] = "Hourly rate must be between ₹0.00 and ₹2000.00.";
            return RedirectToAction(nameof(Index));
        }

        rate.RatePerKwh = Math.Round(ratePerKwh, 2);
        rate.RatePerHour = ratePerHour.HasValue ? Math.Round(ratePerHour.Value, 2) : null;
        rate.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Charging rate for {rate.ConnectorType} updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /manager/rates/toggle-status
    [HttpPost("toggle-status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        var manager = await GetCurrentManagerAsync();
        if (manager == null || !manager.CanManageRates)
        {
            TempData["Error"] = "Permission Denied: Station Owner has not granted you permission to change rate status.";
            return RedirectToAction(nameof(Index));
        }

        var rate = await _context.ChargingRates
            .FirstOrDefaultAsync(r => r.Id == id && r.StationId == manager.StationId);

        if (rate == null) return NotFound();

        if (!rate.IsActive)
        {
            var otherActive = await _context.ChargingRates
                .AnyAsync(r => r.StationId == rate.StationId && r.ConnectorType == rate.ConnectorType && r.IsActive && r.Id != rate.Id);

            if (otherActive)
            {
                TempData["Error"] = $"Another active rate already exists for '{rate.ConnectorType}'. Deactivate it first.";
                return RedirectToAction(nameof(Index));
            }
        }

        rate.IsActive = !rate.IsActive;
        rate.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var statusMsg = rate.IsActive ? "activated" : "deactivated";
        TempData["Success"] = $"Charging rate for {rate.ConnectorType} has been {statusMsg}.";
        return RedirectToAction(nameof(Index));
    }
}
