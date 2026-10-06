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

    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var userId = GetUserId();
        var manager = await _context.StationManagers
            .Include(m => m.User)
            .Include(m => m.Station)
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (manager == null)
        {
            return RedirectToAction("Error", "Home");
        }

        return View("~/Views/stationmanager/Profile.cshtml", manager);
    }

    [HttpPost("profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(string fullname, string phone)
    {
        var userId = GetUserId();
        var manager = await _context.StationManagers
            .Include(m => m.User)
            .Include(m => m.Station)
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (manager == null)
        {
            return RedirectToAction("Error", "Home");
        }

        var trimmedPhone = phone?.Trim();

        // Check uniqueness for phone in UserMaster
        if (await _context.UserMasters.AnyAsync(u => u.Mobile == trimmedPhone && u.Id != manager.UserId))
        {
            ViewBag.Error = "Mobile number is already in use.";
            return View("~/Views/stationmanager/Profile.cshtml", manager);
        }

        // Update fields in StationManager
        manager.FullName = fullname?.Trim() ?? "";
        manager.Phone = trimmedPhone ?? "";

        // Sync with UserMaster
        if (manager.User != null)
        {
            manager.User.Fullname = manager.FullName;
            manager.User.Mobile = manager.Phone;
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = "Profile updated successfully.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet("change-password")]
    public IActionResult ChangePassword()
    {
        return View("~/Views/stationmanager/ChangePassword.cshtml");
    }

    [HttpPost("change-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            ViewBag.Error = "All fields are required.";
            return View("~/Views/stationmanager/ChangePassword.cshtml");
        }

        if (newPassword != confirmPassword)
        {
            ViewBag.Error = "New password and confirm password do not match.";
            return View("~/Views/stationmanager/ChangePassword.cshtml");
        }

        var userId = GetUserId();
        var user = await _context.UserMasters.FindAsync(userId);
        if (user == null)
        {
            ViewBag.Error = "Account not found.";
            return View("~/Views/stationmanager/ChangePassword.cshtml");
        }

        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<VoltNet.Models.UserMaster>();
        Microsoft.AspNetCore.Identity.PasswordVerificationResult result;
        try
        {
            result = hasher.VerifyHashedPassword(user, user.Password ?? "", currentPassword);
        }
        catch (FormatException)
        {
            result = user.Password == currentPassword
                ? Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success
                : Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed;
        }

        if (result == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
        {
            ViewBag.Error = "Current password is incorrect.";
            return View("~/Views/stationmanager/ChangePassword.cshtml");
        }

        user.Password = hasher.HashPassword(user, newPassword);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction(nameof(ChangePassword));
    }
}
