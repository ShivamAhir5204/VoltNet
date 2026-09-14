using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace VoltNet.Controllers.admin;

[Authorize(Roles = "SuperAdmin,Admin")]
[Route("admin/Dashboard/{action=Index}")]
public class DashboardController : Controller
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.TotalUsers = await _context.UserMasters.CountAsync();
        ViewBag.ActiveUsers = await _context.UserMasters.CountAsync(u => u.Isactive);
        ViewBag.TotalStations = await _context.Stations.CountAsync();
        ViewBag.ActiveStations = await _context.Stations.CountAsync(s => s.Status == "Active");

        if (User.IsInRole("SuperAdmin"))
        {
            ViewBag.TotalAdmins = await _context.AdminUsers.CountAsync(a => a.Role != "SuperAdmin");
        }

        return View("~/Views/admin/dashboard/Index.cshtml");
    }

    [HttpGet]
    public IActionResult ResetPassword()
    {
        return View("~/Views/admin/dashboard/ResetPassword.cshtml");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string currentPassword, string newPassword, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            ViewBag.Error = "All fields are required.";
            return View("~/Views/admin/dashboard/ResetPassword.cshtml");
        }

        if (newPassword != confirmPassword)
        {
            ViewBag.Error = "New password and confirm password do not match.";
            return View("~/Views/admin/dashboard/ResetPassword.cshtml");
        }

        var adminId = User.FindFirstValue("AdminId");
        if (adminId == null || !Guid.TryParse(adminId, out var id))
        {
            ViewBag.Error = "Unable to identify your account.";
            return View("~/Views/admin/dashboard/ResetPassword.cshtml");
        }

        var admin = await _context.AdminUsers.FindAsync(id);
        if (admin == null)
        {
            ViewBag.Error = "Admin account not found.";
            return View("~/Views/admin/dashboard/ResetPassword.cshtml");
        }

        var hasher = new PasswordHasher<AdminUser>();
        PasswordVerificationResult result;
        try
        {
            result = hasher.VerifyHashedPassword(admin, admin.Password, currentPassword);
        }
        catch (FormatException)
        {
            result = admin.Password == currentPassword
                ? PasswordVerificationResult.Success
                : PasswordVerificationResult.Failed;
        }

        if (result == PasswordVerificationResult.Failed)
        {
            ViewBag.Error = "Current password is incorrect.";
            return View("~/Views/admin/dashboard/ResetPassword.cshtml");
        }

        admin.Password = hasher.HashPassword(admin, newPassword);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction(nameof(ResetPassword));
    }
}
