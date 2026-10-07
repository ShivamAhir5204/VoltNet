using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace VoltNet.Controllers.stationowner;

[Authorize(Roles = "StationOwner")]
[Route("owner/rates")]
public class OwnerRatesController : Controller
{
    private readonly AppDbContext _context;

    public OwnerRatesController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue("UserId")!);

    // GET: /owner/rates
    [HttpGet("")]
    public async Task<IActionResult> IndexAll()
    {
        var userId = GetUserId();
        var stations = await _context.Stations
            .Include(s => s.Chargers)
            .Include(s => s.ChargingRates)
            .Where(s => s.OwnerUserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return View("~/Views/stationowner/Rates/StationsList.cshtml", stations);
    }

    // GET: /owner/rates/{stationId}
    [HttpGet("{stationId}")]
    public async Task<IActionResult> Index(Guid stationId)
    {
        var userId = GetUserId();

        // 1. Ownership / IDOR validation
        var station = await _context.Stations
            .Include(s => s.Chargers)
            .Include(s => s.ChargingRates)
            .FirstOrDefaultAsync(s => s.Id == stationId && s.OwnerUserId == userId);

        if (station == null)
        {
            TempData["Error"] = "Station not found or you do not have permission to access it.";
            return RedirectToAction("Index", "OwnerStation");
        }

        // 2. Identify available connector types from existing chargers at this station
        var availableConnectors = station.Chargers
            .Select(c => c.ConnectorType)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        ViewBag.Station = station;
        ViewBag.AvailableConnectors = availableConnectors;

        // Pass existing rates ordered by active status then connector type
        var rates = station.ChargingRates
            .OrderByDescending(r => r.IsActive)
            .ThenBy(r => r.ConnectorType)
            .ToList();

        return View("~/Views/stationowner/Rates/Index.cshtml", rates);
    }

    // POST: /owner/rates/create
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid stationId, string connectorType, decimal ratePerKwh, decimal? ratePerHour)
    {
        var userId = GetUserId();

        var station = await _context.Stations
            .Include(s => s.Chargers)
            .Include(s => s.ChargingRates)
            .FirstOrDefaultAsync(s => s.Id == stationId && s.OwnerUserId == userId);

        if (station == null) return NotFound();

        // Server-side validations
        if (string.IsNullOrWhiteSpace(connectorType))
        {
            TempData["Error"] = "Please select a valid connector type.";
            return RedirectToAction(nameof(Index), new { stationId });
        }

        var trimmedConnector = connectorType.Trim();

        // Check that station actually has chargers with this connector type
        if (!station.Chargers.Any(c => c.ConnectorType.Equals(trimmedConnector, StringComparison.OrdinalIgnoreCase)))
        {
            TempData["Error"] = $"Cannot add rate: Station does not have any '{trimmedConnector}' chargers installed.";
            return RedirectToAction(nameof(Index), new { stationId });
        }

        // Price bounds check (e.g. anti-typo: ₹0.01 to ₹150.00/kWh)
        if (ratePerKwh <= 0 || ratePerKwh > 150)
        {
            TempData["Error"] = "Rate per kWh must be greater than ₹0.00 and no more than ₹150.00.";
            return RedirectToAction(nameof(Index), new { stationId });
        }

        if (ratePerHour.HasValue && (ratePerHour.Value < 0 || ratePerHour.Value > 2000))
        {
            TempData["Error"] = "Hourly rate must be between ₹0.00 and ₹2000.00.";
            return RedirectToAction(nameof(Index), new { stationId });
        }

        // Single Active Rate per Connector Rule:
        var existingActive = await _context.ChargingRates
            .FirstOrDefaultAsync(r => r.StationId == stationId && r.ConnectorType == trimmedConnector && r.IsActive);

        if (existingActive != null)
        {
            TempData["Error"] = $"An active rate for '{trimmedConnector}' already exists (₹{existingActive.RatePerKwh:F2}/kWh). Please edit or deactivate the existing rate first.";
            return RedirectToAction(nameof(Index), new { stationId });
        }

        var newRate = new ChargingRate
        {
            Id = Guid.NewGuid(),
            StationId = stationId,
            ConnectorType = trimmedConnector,
            RatePerKwh = Math.Round(ratePerKwh, 2),
            RatePerHour = ratePerHour.HasValue ? Math.Round(ratePerHour.Value, 2) : null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.ChargingRates.Add(newRate);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Charging rate for {trimmedConnector} (₹{newRate.RatePerKwh:F2}/kWh) created successfully.";
        return RedirectToAction(nameof(Index), new { stationId });
    }

    // POST: /owner/rates/edit
    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, decimal ratePerKwh, decimal? ratePerHour)
    {
        var userId = GetUserId();

        var rate = await _context.ChargingRates
            .Include(r => r.Station)
            .FirstOrDefaultAsync(r => r.Id == id && r.Station!.OwnerUserId == userId);

        if (rate == null)
            return NotFound();

        // Validations
        if (ratePerKwh <= 0 || ratePerKwh > 150)
        {
            TempData["Error"] = "Rate per kWh must be greater than ₹0.00 and no more than ₹150.00.";
            return RedirectToAction(nameof(Index), new { stationId = rate.StationId });
        }

        if (ratePerHour.HasValue && (ratePerHour.Value < 0 || ratePerHour.Value > 2000))
        {
            TempData["Error"] = "Hourly rate must be between ₹0.00 and ₹2000.00.";
            return RedirectToAction(nameof(Index), new { stationId = rate.StationId });
        }

        rate.RatePerKwh = Math.Round(ratePerKwh, 2);
        rate.RatePerHour = ratePerHour.HasValue ? Math.Round(ratePerHour.Value, 2) : null;
        rate.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Charging rate for {rate.ConnectorType} updated successfully.";
        return RedirectToAction(nameof(Index), new { stationId = rate.StationId });
    }

    // POST: /owner/rates/toggle-status
    [HttpPost("toggle-status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        var userId = GetUserId();

        var rate = await _context.ChargingRates
            .Include(r => r.Station)
            .FirstOrDefaultAsync(r => r.Id == id && r.Station!.OwnerUserId == userId);

        if (rate == null)
            return NotFound();

        // If activating, verify no other active rate exists for the same connector
        if (!rate.IsActive)
        {
            var otherActive = await _context.ChargingRates
                .AnyAsync(r => r.StationId == rate.StationId && r.ConnectorType == rate.ConnectorType && r.IsActive && r.Id != rate.Id);

            if (otherActive)
            {
                TempData["Error"] = $"Another active rate already exists for '{rate.ConnectorType}'. Deactivate it first before activating this rate.";
                return RedirectToAction(nameof(Index), new { stationId = rate.StationId });
            }
        }

        rate.IsActive = !rate.IsActive;
        rate.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var statusMsg = rate.IsActive ? "activated" : "deactivated";
        TempData["Success"] = $"Charging rate for {rate.ConnectorType} has been {statusMsg}.";
        return RedirectToAction(nameof(Index), new { stationId = rate.StationId });
    }
}
