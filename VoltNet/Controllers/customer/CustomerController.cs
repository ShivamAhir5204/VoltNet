using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VoltNet.Data;
using VoltNet.Models;
using Microsoft.AspNetCore.Identity;

namespace VoltNet.Controllers.customer;

[Authorize(Roles = "Customer")]
public class CustomerController : Controller
{
    private readonly AppDbContext _context;

    public CustomerController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirstValue("UserId")!);
    }

    [HttpGet("customer/dashboard")]
    public IActionResult Dashboard()
    {
        return View("~/Views/customer/Dashboard.cshtml");
    }

    // ── Profile ──────────────────────────────────────────────────

    [HttpGet("customer/profile")]
    public async Task<IActionResult> Profile()
    {
        var userId = GetUserId();
        var user = await _context.UserMasters.FindAsync(userId);

        if (user == null)
        {
            return RedirectToAction(nameof(Dashboard));
        }

        return View("~/Views/customer/Profile.cshtml", user);
    }

    [HttpPost("customer/profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(string fullname, string email, string mobile, string city, string state)
    {
        var userId = GetUserId();
        var user = await _context.UserMasters.FindAsync(userId);

        if (user == null)
        {
            ViewBag.Error = "Account not found.";
            return View("~/Views/customer/Profile.cshtml", new UserMaster());
        }

        // Validation
        if (string.IsNullOrWhiteSpace(fullname) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(mobile))
        {
            ViewBag.Error = "Full Name, Email and Mobile are required.";
            return View("~/Views/customer/Profile.cshtml", user);
        }

        var trimmedEmail = email.Trim();
        var trimmedMobile = mobile.Trim();

        // Check for duplicate email (excluding current user)
        var duplicateEmail = await _context.UserMasters
            .AnyAsync(u => u.Id != userId && u.Email == trimmedEmail);
        if (duplicateEmail)
        {
            ViewBag.Error = "This email is already registered by another user.";
            return View("~/Views/customer/Profile.cshtml", user);
        }

        // Check for duplicate mobile (excluding current user)
        var duplicateMobile = await _context.UserMasters
            .AnyAsync(u => u.Id != userId && u.Mobile == trimmedMobile);
        if (duplicateMobile)
        {
            ViewBag.Error = "This mobile number is already registered by another user.";
            return View("~/Views/customer/Profile.cshtml", user);
        }

        // Update fields
        user.Fullname = fullname.Trim();
        user.Email = trimmedEmail;
        user.Mobile = trimmedMobile;
        user.City = city?.Trim();
        user.State = state?.Trim();

        await _context.SaveChangesAsync();

        TempData["Success"] = "Profile updated successfully.";
        return Redirect("/customer/profile");
    }

    // ── Change Password ──────────────────────────────────────────

    [HttpGet("customer/change-password")]
    public IActionResult ChangePassword()
    {
        return View("~/Views/customer/ChangePassword.cshtml");
    }

    [HttpPost("customer/change-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            ViewBag.Error = "All fields are required.";
            return View("~/Views/customer/ChangePassword.cshtml");
        }

        if (newPassword.Length < 6)
        {
            ViewBag.Error = "New password must be at least 6 characters.";
            return View("~/Views/customer/ChangePassword.cshtml");
        }

        if (newPassword != confirmPassword)
        {
            ViewBag.Error = "New password and confirm password do not match.";
            return View("~/Views/customer/ChangePassword.cshtml");
        }

        var userId = GetUserId();
        var user = await _context.UserMasters.FindAsync(userId);
        if (user == null)
        {
            ViewBag.Error = "Account not found.";
            return View("~/Views/customer/ChangePassword.cshtml");
        }

        var hasher = new PasswordHasher<UserMaster>();
        PasswordVerificationResult result;
        try
        {
            result = hasher.VerifyHashedPassword(user, user.Password ?? "", currentPassword);
        }
        catch (FormatException)
        {
            result = user.Password == currentPassword
                ? PasswordVerificationResult.Success
                : PasswordVerificationResult.Failed;
        }

        if (result == PasswordVerificationResult.Failed)
        {
            ViewBag.Error = "Current password is incorrect.";
            return View("~/Views/customer/ChangePassword.cshtml");
        }

        user.Password = hasher.HashPassword(user, newPassword);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Password changed successfully.";
        return Redirect("/customer/change-password");
    }
}
