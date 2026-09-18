using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VoltNet.Data;

namespace VoltNet.Controllers.stationmanager;

[Authorize(Roles = "StationManager")]
[Route("manager/dashboard")]
public class ManagerDashboardController : Controller
{
    private readonly AppDbContext _context;

    public ManagerDashboardController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirstValue("UserId")!);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();
        var manager = await _context.StationManagers
            .Include(m => m.Station)
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (manager == null || manager.Station == null)
        {
            return RedirectToAction("Error", "Home");
        }

        var totalChargers = await _context.Chargers.CountAsync(c => c.StationId == manager.StationId);
        var activeChargers = await _context.Chargers.CountAsync(c => c.StationId == manager.StationId && c.Status == "Active");
        
        ViewBag.TotalChargers = totalChargers;
        ViewBag.ActiveChargers = activeChargers;
        ViewBag.StationName = manager.Station.Name;
        ViewBag.StationStatus = manager.Station.Status;

        return View("~/Views/stationmanager/Dashboard.cshtml");
    }
}
