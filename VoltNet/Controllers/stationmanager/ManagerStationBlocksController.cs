using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VoltNet.Data;
using VoltNet.Models;

namespace VoltNet.Controllers.stationmanager;

[Authorize(Roles = "StationManager")]
[Route("manager/station-blocks")]
public class ManagerStationBlocksController : Controller
{
    private readonly AppDbContext _context;

    public ManagerStationBlocksController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue("UserId")!);

    private async Task<StationManager?> GetCurrentManagerAsync()
    {
        var userId = GetUserId();
        return await _context.StationManagers
            .Include(m => m.Station)
            .ThenInclude(s => s!.Chargers)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.IsActive);
    }

    // GET: /manager/station-blocks
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var manager = await GetCurrentManagerAsync();
        if (manager == null || manager.Station == null)
            return RedirectToAction("Error", "Home");

        var today = DateTime.UtcNow.Date;
        var blocks = await _context.StationBlockSlots
            .Include(b => b.Charger)
            .Where(b => b.StationId == manager.StationId && b.BlockDate >= today.AddDays(-1))
            .OrderByDescending(b => b.BlockDate)
            .ThenBy(b => b.StartTime)
            .ToListAsync();

        ViewBag.Station = manager.Station;
        ViewBag.Chargers = manager.Station.Chargers.Where(c => c.Status == "Active").ToList();

        return View("~/Views/stationmanager/StationBlocks.cshtml", blocks);
    }

    // POST: /manager/station-blocks/create
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid? chargerId, DateTime blockDate, string startTime, string endTime, string reason, string? remarks)
    {
        var manager = await GetCurrentManagerAsync();
        if (manager == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(startTime) || string.IsNullOrWhiteSpace(endTime))
        {
            TempData["Error"] = "Start time and end time are required.";
            return RedirectToAction(nameof(Index));
        }

        var start = TimeSpan.Parse(startTime);
        var end = TimeSpan.Parse(endTime);

        if (end <= start)
        {
            TempData["Error"] = "End time must be after start time.";
            return RedirectToAction(nameof(Index));
        }

        var block = new StationBlockSlot
        {
            Id = Guid.NewGuid(),
            StationId = manager.StationId,
            ChargerId = chargerId,
            BlockDate = blockDate.Date,
            StartTime = start,
            EndTime = end,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Maintenance" : reason.Trim(),
            Remarks = remarks?.Trim(),
            CreatedByManagerId = GetUserId(),
            CreatedAt = DateTime.UtcNow
        };

        _context.StationBlockSlots.Add(block);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Slot successfully blocked for {block.Reason} on {block.BlockDate:dd MMM yyyy} ({start:hh\\:mm} - {end:hh\\:mm}).";
        return RedirectToAction(nameof(Index));
    }

    // POST: /manager/station-blocks/delete
    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var manager = await GetCurrentManagerAsync();
        if (manager == null) return Unauthorized();

        var block = await _context.StationBlockSlots
            .FirstOrDefaultAsync(b => b.Id == id && b.StationId == manager.StationId);

        if (block != null)
        {
            _context.StationBlockSlots.Remove(block);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Blocked slot released. Available for customer booking again.";
        }

        return RedirectToAction(nameof(Index));
    }
}
