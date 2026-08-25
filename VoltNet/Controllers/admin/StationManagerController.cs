using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using Microsoft.AspNetCore.Authorization;

namespace VoltNet.Controllers.admin;

[Authorize(Roles = "SuperAdmin,Admin")]
[Route("admin/StationManager/{action=Index}/{id?}")]
public class StationManagerController : Controller
{
    private readonly AppDbContext _context;

    public StationManagerController(AppDbContext context)
    {
        _context = context;
    }

    // GET: admin/StationManager
    public async Task<IActionResult> Index()
    {
        var managers = await _context.StationManagers
            .Include(sm => sm.Station)
            .Include(sm => sm.User)
            .OrderByDescending(sm => sm.AssignedAt)
            .ToListAsync();
        return View("~/Views/admin/stationmanager/Index.cshtml", managers);
    }

    // GET: admin/StationManager/Create
    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        return View("~/Views/admin/stationmanager/Create.cshtml");
    }

    // POST: admin/StationManager/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("StationId,UserId")] StationManager stationManager)
    {
        if (ModelState.IsValid)
        {
            // Check if already assigned
            bool exists = await _context.StationManagers.AnyAsync(sm => sm.StationId == stationManager.StationId && sm.UserId == stationManager.UserId);
            if (exists)
            {
                ModelState.AddModelError("", "This manager is already assigned to this station.");
                await PopulateDropdowns(stationManager.StationId, stationManager.UserId);
                return View("~/Views/admin/stationmanager/Create.cshtml", stationManager);
            }

            stationManager.Id = Guid.NewGuid();
            stationManager.AssignedAt = DateTime.UtcNow;
            _context.Add(stationManager);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        await PopulateDropdowns(stationManager.StationId, stationManager.UserId);
        return View("~/Views/admin/stationmanager/Create.cshtml", stationManager);
    }

    // POST: admin/StationManager/LoadEdit (No ID in URL)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadEdit(Guid id)
    {
        var stationManager = await _context.StationManagers.FindAsync(id);
        if (stationManager == null)
            return NotFound();

        await PopulateDropdowns(stationManager.StationId, stationManager.UserId);
        return View("~/Views/admin/stationmanager/Edit.cshtml", stationManager);
    }

    // POST: admin/StationManager/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([Bind("Id,StationId,UserId,AssignedAt")] StationManager stationManager)
    {
        if (ModelState.IsValid)
        {
            // Check for duplicates (excluding self)
            bool exists = await _context.StationManagers.AnyAsync(sm => sm.StationId == stationManager.StationId && sm.UserId == stationManager.UserId && sm.Id != stationManager.Id);
            if (exists)
            {
                ModelState.AddModelError("", "This manager is already assigned to this station.");
                await PopulateDropdowns(stationManager.StationId, stationManager.UserId);
                return View("~/Views/admin/stationmanager/Edit.cshtml", stationManager);
            }

            try
            {
                _context.Update(stationManager);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StationManagerExists(stationManager.Id))
                    return NotFound();
                else
                    throw;
            }
            return RedirectToAction(nameof(Index));
        }
        await PopulateDropdowns(stationManager.StationId, stationManager.UserId);
        return View("~/Views/admin/stationmanager/Edit.cshtml", stationManager);
    }

    // POST: admin/StationManager/Delete
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var stationManager = await _context.StationManagers.FindAsync(id);
        if (stationManager != null)
        {
            _context.StationManagers.Remove(stationManager);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private bool StationManagerExists(Guid id)
    {
        return _context.StationManagers.Any(e => e.Id == id);
    }

    private async Task PopulateDropdowns(Guid? selectedStation = null, Guid? selectedUser = null)
    {
        var stations = await _context.Stations.OrderBy(s => s.Name).ToListAsync();
        ViewBag.StationList = new SelectList(stations, "Id", "Name", selectedStation);

        var users = await _context.UserMasters
            .Where(u => u.Role == "StationManager" || u.Role == "Admin") // Assuming these roles
            .OrderBy(u => u.Fullname)
            .ToListAsync();
        ViewBag.UserList = new SelectList(users, "Id", "Fullname", selectedUser);
    }
}
