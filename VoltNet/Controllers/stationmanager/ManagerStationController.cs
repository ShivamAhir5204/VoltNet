using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VoltNet.Data;

namespace VoltNet.Controllers.stationmanager;

[Authorize(Roles = "StationManager")]
[Route("manager/station")]
public class ManagerStationController : Controller
{
    private readonly AppDbContext _context;

    public ManagerStationController(AppDbContext context)
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

        var station = await _context.Stations.FirstOrDefaultAsync(s => s.Id == stationId);
        if (station == null) return NotFound();

        return View("~/Views/stationmanager/StationDetails.cshtml", station);
    }

    [HttpPost("update-status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(string status)
    {
        var stationId = await GetStationIdAsync(GetUserId());
        if (stationId == null) return RedirectToAction("Error", "Home");

        var station = await _context.Stations.FirstOrDefaultAsync(s => s.Id == stationId);
        if (station == null) return NotFound();

        if (status == "Active" || status == "Maintenance" || status == "Offline")
        {
            station.Status = status;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Station status updated successfully.";
        }

        return RedirectToAction("Index");
    }
}
