using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace VoltNet.Controllers.stationowner;

[Authorize(Roles = "StationOwner")]
[Route("owner")]
public class OwnerDashboardController : Controller
{
    private readonly AppDbContext _context;

    public OwnerDashboardController(AppDbContext context)
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

    [HttpGet("")]
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var ownerId = GetStationOwnerId();
        var userId = GetUserId();

        if (ownerId == null)
        {
            // StationOwner profile not yet created — edge case
            ViewBag.Error = "Your Station Owner profile is not set up yet. Please contact the administrator.";
            return View("~/Views/stationowner/Dashboard.cshtml");
        }

        // Get stations owned by this user (Station.OwnerUserId → UserMaster.Id)
        var stations = await _context.Stations
            .Where(s => s.OwnerUserId == userId)
            .ToListAsync();

        ViewBag.TotalStations = stations.Count;
        ViewBag.ActiveStations = stations.Count(s => s.Status == "Active");
        ViewBag.PendingStations = stations.Count(s => s.Status == "PendingVerification");

        // Get managers created by this owner
        var totalManagers = await _context.StationManagers
            .CountAsync(m => m.CreatedBy == ownerId.Value);
        var activeManagers = await _context.StationManagers
            .CountAsync(m => m.CreatedBy == ownerId.Value && m.IsActive);

        ViewBag.TotalManagers = totalManagers;
        ViewBag.ActiveManagers = activeManagers;

        // Recent stations
        ViewBag.RecentStations = stations.OrderByDescending(s => s.CreatedAt).Take(5).ToList();

        return View("~/Views/stationowner/Dashboard.cshtml");
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var ownerId = GetStationOwnerId();
        if (ownerId == null) return RedirectToAction(nameof(Dashboard));

        var owner = await _context.StationOwners
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == ownerId);

        if (owner == null) return NotFound();

        return View("~/Views/stationowner/Profile.cshtml", owner);
    }

    [HttpPost("profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(string fullname, string email, string phone, string businessName, string businessRegistrationNumber, string address, string city, string state, string gstNumber)
    {
        var ownerId = GetStationOwnerId();
        if (ownerId == null) return RedirectToAction(nameof(Dashboard));

        var owner = await _context.StationOwners
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == ownerId);

        if (owner == null) return NotFound();

        var trimmedEmail = email?.Trim().ToLower();
        var trimmedPhone = phone?.Trim();

        // Check uniqueness for email in UserMaster
        if (await _context.UserMasters.AnyAsync(u => u.Email == trimmedEmail && u.Id != owner.UserId))
        {
            ViewBag.Error = "Email address is already in use.";
            return View("~/Views/stationowner/Profile.cshtml", owner);
        }

        // Check uniqueness for phone in UserMaster
        if (await _context.UserMasters.AnyAsync(u => u.Mobile == trimmedPhone && u.Id != owner.UserId))
        {
            ViewBag.Error = "Mobile number is already in use.";
            return View("~/Views/stationowner/Profile.cshtml", owner);
        }

        // Update fields in StationOwner
        owner.FullName = fullname?.Trim() ?? "";
        owner.Phone = trimmedPhone ?? "";
        owner.BusinessName = businessName?.Trim();
        owner.BusinessRegistrationNumber = businessRegistrationNumber?.Trim();
        owner.Address = address?.Trim();
        owner.City = city?.Trim();
        owner.State = state?.Trim();
        owner.GSTNumber = gstNumber?.Trim();

        // Sync with UserMaster
        owner.User.Fullname = owner.FullName;
        owner.User.Email = trimmedEmail;
        owner.User.Mobile = owner.Phone;
        owner.User.City = owner.City;
        owner.User.State = owner.State;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Profile updated successfully.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet("change-password")]
    public IActionResult ChangePassword()
    {
        return View("~/Views/stationowner/ChangePassword.cshtml");
    }

    [HttpPost("change-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            ViewBag.Error = "All fields are required.";
            return View("~/Views/stationowner/ChangePassword.cshtml");
        }

        if (newPassword != confirmPassword)
        {
            ViewBag.Error = "New password and confirm password do not match.";
            return View("~/Views/stationowner/ChangePassword.cshtml");
        }

        var userId = GetUserId();
        var user = await _context.UserMasters.FindAsync(userId);
        if (user == null)
        {
            ViewBag.Error = "Account not found.";
            return View("~/Views/stationowner/ChangePassword.cshtml");
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
            return View("~/Views/stationowner/ChangePassword.cshtml");
        }

        user.Password = hasher.HashPassword(user, newPassword);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction(nameof(ChangePassword));
    }
}
