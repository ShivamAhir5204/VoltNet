using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using Microsoft.AspNetCore.Authorization;

namespace VoltNet.Controllers.admin;

[Authorize(Roles = "SuperAdmin")]
[Route("admin/StationManagerAssign/{action=Index}/{id?}")]
public class StationManagerAssignController : Controller
{
    private readonly AppDbContext _context;

    public StationManagerAssignController(AppDbContext context)
    {
        _context = context;
    }

    // GET: admin/StationManagerAssign
    public async Task<IActionResult> Index()
    {
        var assignments = await _context.StationManagers
            .Include(sm => sm.Station)
            .Include(sm => sm.User)
            .ToListAsync();
        return View("~/Views/admin/stationmanager/Index.cshtml", assignments);
    }

    // POST: admin/StationManagerAssign/LoadDetails
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadDetails(int id)
    {
        var assignment = await _context.StationManagers
            .Include(sm => sm.Station)
            .Include(sm => sm.User)
            .FirstOrDefaultAsync(sm => sm.Id == id);
        if (assignment == null)
            return NotFound();

        return View("~/Views/admin/stationmanager/Details.cshtml", assignment);
    }

    // GET: admin/StationManagerAssign/Create
    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        return View("~/Views/admin/stationmanager/Create.cshtml");
    }

    // POST: admin/StationManagerAssign/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("StationId,UserId")] StationManager assignment)
    {
        // Check for duplicate assignment
        var exists = await _context.StationManagers
            .AnyAsync(sm => sm.StationId == assignment.StationId && sm.UserId == assignment.UserId);
        if (exists)
        {
            ModelState.AddModelError("", "This user is already assigned to this station.");
        }

        if (ModelState.IsValid)
        {
            assignment.AssignedAt = DateTime.UtcNow;
            _context.Add(assignment);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        await PopulateDropdowns(assignment.StationId, assignment.UserId);
        return View("~/Views/admin/stationmanager/Create.cshtml", assignment);
    }

    // POST: admin/StationManagerAssign/LoadEdit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadEdit(int id)
    {
        var assignment = await _context.StationManagers.FindAsync(id);
        if (assignment == null)
            return NotFound();

        await PopulateDropdowns(assignment.StationId, assignment.UserId);
        return View("~/Views/admin/stationmanager/Edit.cshtml", assignment);
    }

    // POST: admin/StationManagerAssign/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,StationId,UserId,AssignedAt")] StationManager assignment)
    {
        if (id != assignment.Id)
        {
            return NotFound();
        }

        // Check for duplicate assignment (excluding current record)
        var duplicate = await _context.StationManagers
            .AnyAsync(sm => sm.StationId == assignment.StationId && sm.UserId == assignment.UserId && sm.Id != id);
        if (duplicate)
        {
            ModelState.AddModelError("", "This user is already assigned to this station.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(assignment);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AssignmentExists(assignment.Id))
                    return NotFound();
                else
                    throw;
            }
            return RedirectToAction(nameof(Index));
        }
        await PopulateDropdowns(assignment.StationId, assignment.UserId);
        return View("~/Views/admin/stationmanager/Edit.cshtml", assignment);
    }

    // POST: admin/StationManagerAssign/Delete
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var assignment = await _context.StationManagers.FindAsync(id);
        if (assignment != null)
        {
            _context.StationManagers.Remove(assignment);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private bool AssignmentExists(int id)
    {
        return _context.StationManagers.Any(e => e.Id == id);
    }

    private async Task PopulateDropdowns(int? selectedStationId = null, Guid? selectedUserId = null)
    {
        var stations = await _context.Stations
            .Where(s => s.Status == "Active")
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.Name + " (" + s.City + ")",
                Selected = selectedStationId.HasValue && s.Id == selectedStationId.Value
            })
            .ToListAsync();

        var managers = await _context.UserMasters
            .Where(u => u.Role == "StationManager" && u.Isactive)
            .OrderBy(u => u.Fullname)
            .Select(u => new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = u.Fullname + " (" + u.Email + ")",
                Selected = selectedUserId.HasValue && u.Id == selectedUserId.Value
            })
            .ToListAsync();

        ViewBag.Stations = stations;
        ViewBag.Managers = managers;
    }
}
