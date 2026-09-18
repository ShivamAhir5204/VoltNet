using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace VoltNet.Controllers.stationowner;

[Authorize(Roles = "StationOwner")]
[Route("owner/station-managers/{action=Index}/{id?}")]
public class OwnerStationManagerController : Controller
{
    private readonly AppDbContext _context;

    public OwnerStationManagerController(AppDbContext context)
    {
        _context = context;
    }

    private Guid? GetStationOwnerId()
    {
        var claim = User.FindFirstValue("StationOwnerId");
        return claim != null && Guid.TryParse(claim, out var id) ? id : null;
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirstValue("UserId")!);
    }

    // GET: owner/station-managers
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var ownerId = GetStationOwnerId();
        if (ownerId == null) return Unauthorized();

        var managers = await _context.StationManagers
            .Include(m => m.User)
            .Include(m => m.Station)
            .Where(m => m.CreatedBy == ownerId.Value)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        return View("~/Views/stationowner/StationManagers.cshtml", managers);
    }

    // GET: owner/station-managers/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var userId = GetUserId();
        await PopulateStationDropdown(userId);
        return View("~/Views/stationowner/CreateStationManager.cshtml");
    }

    // POST: owner/station-managers/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string fullName, string email, string phone, string password, Guid stationId)
    {
        var ownerId = GetStationOwnerId();
        var userId = GetUserId();

        if (ownerId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(password) ||
            stationId == Guid.Empty)
        {
            ViewBag.Error = "All fields are required.";
            await PopulateStationDropdown(userId, stationId);
            return View("~/Views/stationowner/CreateStationManager.cshtml");
        }

        // Verify the station belongs to this owner
        var station = await _context.Stations
            .FirstOrDefaultAsync(s => s.Id == stationId && s.OwnerUserId == userId);
        if (station == null)
        {
            ViewBag.Error = "Invalid station selected.";
            await PopulateStationDropdown(userId, stationId);
            return View("~/Views/stationowner/CreateStationManager.cshtml");
        }

        // Check if email is already taken
        var existingUser = await _context.UserMasters
            .FirstOrDefaultAsync(u => u.Email == email.Trim());
        if (existingUser != null)
        {
            ViewBag.Error = "A user with this email already exists.";
            await PopulateStationDropdown(userId, stationId);
            return View("~/Views/stationowner/CreateStationManager.cshtml");
        }

        // Create UserMaster row for the manager
        var managerUser = new UserMaster
        {
            Id = Guid.NewGuid(),
            Fullname = fullName.Trim(),
            Email = email.Trim(),
            Mobile = phone.Trim(),
            Role = "StationManager",
            Isactive = true,
            CreatedAt = DateTime.UtcNow
        };

        var hasher = new PasswordHasher<UserMaster>();
        managerUser.Password = hasher.HashPassword(managerUser, password);

        _context.UserMasters.Add(managerUser);

        // Create StationManager profile row
        var stationManager = new StationManager
        {
            Id = Guid.NewGuid(),
            StationId = stationId,
            UserId = managerUser.Id,
            FullName = fullName.Trim(),
            Phone = phone.Trim(),
            CreatedBy = ownerId.Value,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.StationManagers.Add(stationManager);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Station Manager '{fullName.Trim()}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    // POST: owner/station-managers/Deactivate
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var ownerId = GetStationOwnerId();
        if (ownerId == null) return Unauthorized();

        var manager = await _context.StationManagers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == id && m.CreatedBy == ownerId.Value);

        if (manager == null)
            return NotFound();

        manager.IsActive = false;
        if (manager.User != null)
            manager.User.Isactive = false;

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Station Manager '{manager.FullName}' has been deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // POST: owner/station-managers/Activate
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(Guid id)
    {
        var ownerId = GetStationOwnerId();
        if (ownerId == null) return Unauthorized();

        var manager = await _context.StationManagers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == id && m.CreatedBy == ownerId.Value);

        if (manager == null)
            return NotFound();

        manager.IsActive = true;
        if (manager.User != null)
            manager.User.Isactive = true;

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Station Manager '{manager.FullName}' has been activated.";
        return RedirectToAction(nameof(Index));
    }

    // GET: owner/station-managers/Edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var ownerId = GetStationOwnerId();
        var userId = GetUserId();
        if (ownerId == null) return Unauthorized();

        var manager = await _context.StationManagers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == id && m.CreatedBy == ownerId.Value);

        if (manager == null)
            return NotFound();

        await PopulateStationDropdown(userId, manager.StationId);
        return View("~/Views/stationowner/EditStationManager.cshtml", manager);
    }

    // POST: owner/station-managers/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, string fullName, string phone, Guid stationId)
    {
        var ownerId = GetStationOwnerId();
        var userId = GetUserId();
        if (ownerId == null) return Unauthorized();

        var manager = await _context.StationManagers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == id && m.CreatedBy == ownerId.Value);

        if (manager == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(phone) || stationId == Guid.Empty)
        {
            ViewBag.Error = "Full Name, Phone number, and Station are required.";
            await PopulateStationDropdown(userId, stationId);
            return View("~/Views/stationowner/EditStationManager.cshtml", manager);
        }

        // Validate phone
        if (phone.Trim().Length != 10 || !phone.Trim().All(char.IsDigit))
        {
            ViewBag.Error = "Please enter a valid 10-digit mobile number.";
            await PopulateStationDropdown(userId, stationId);
            return View("~/Views/stationowner/EditStationManager.cshtml", manager);
        }

        // Verify the assigned station belongs to this owner
        var station = await _context.Stations
            .FirstOrDefaultAsync(s => s.Id == stationId && s.OwnerUserId == userId);
        if (station == null)
        {
            ViewBag.Error = "Invalid station selected.";
            await PopulateStationDropdown(userId, stationId);
            return View("~/Views/stationowner/EditStationManager.cshtml", manager);
        }

        manager.FullName = fullName.Trim();
        manager.Phone = phone.Trim();
        manager.StationId = stationId;

        if (manager.User != null)
        {
            manager.User.Fullname = fullName.Trim();
            manager.User.Mobile = phone.Trim();
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Station Manager '{manager.FullName}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // POST: owner/station-managers/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ownerId = GetStationOwnerId();
        if (ownerId == null) return Unauthorized();

        var manager = await _context.StationManagers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == id && m.CreatedBy == ownerId.Value);

        if (manager == null)
            return NotFound();

        var managerName = manager.FullName;

        // Remove manager profile
        _context.StationManagers.Remove(manager);

        // Remove login UserMaster account if exists
        if (manager.User != null)
        {
            _context.UserMasters.Remove(manager.User);
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Station Manager '{managerName}' deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateStationDropdown(Guid userId, Guid? selectedId = null)
    {
        var stations = await _context.Stations
            .Where(s => s.OwnerUserId == userId && s.Status == "Active")
            .OrderBy(s => s.Name)
            .ToListAsync();

        ViewBag.StationList = new SelectList(stations, "Id", "Name", selectedId);
    }
}
