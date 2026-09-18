using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using System.Security.Claims;

namespace VoltNet.Controllers;

[Authorize(Roles = "Customer")]
public class PartnerController : Controller
{
    private readonly AppDbContext _context;

    public PartnerController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirstValue("UserId")!);
    }

    [HttpGet("partner/apply")]
    public async Task<IActionResult> Apply()
    {
        var userId = GetUserId();

        // Check if already applied
        var existing = await _context.StationOwners.FirstOrDefaultAsync(o => o.UserId == userId);
        if (existing != null)
        {
            return RedirectToAction(nameof(Status));
        }

        var user = await _context.UserMasters.FindAsync(userId);
        ViewBag.UserFullName = user?.Fullname ?? "";
        ViewBag.UserEmail = user?.Email ?? "";
        ViewBag.UserPhone = user?.Mobile ?? "";

        return View("~/Views/Partner/Apply.cshtml");
    }

    [HttpPost("partner/apply")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(
        string fullName, string? businessName, string phone,
        string? businessRegistrationNumber, string? address,
        string? city, string? state, string? gstNumber)
    {
        var userId = GetUserId();

        // Check if already applied
        var existing = await _context.StationOwners.FirstOrDefaultAsync(o => o.UserId == userId);
        if (existing != null)
        {
            return RedirectToAction(nameof(Status));
        }

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(phone))
        {
            ViewBag.Error = "Full Name and Phone are required.";
            return View("~/Views/Partner/Apply.cshtml");
        }

        var owner = new StationOwner
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = fullName.Trim(),
            BusinessName = businessName?.Trim(),
            Phone = phone.Trim(),
            BusinessRegistrationNumber = businessRegistrationNumber?.Trim(),
            Address = address?.Trim(),
            City = city?.Trim(),
            State = state?.Trim(),
            GSTNumber = gstNumber?.Trim(),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.StationOwners.Add(owner);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Your partner application has been submitted successfully! We will review it shortly.";
        return RedirectToAction(nameof(Status));
    }

    [HttpGet("partner/status")]
    public async Task<IActionResult> Status()
    {
        var userId = GetUserId();

        var owner = await _context.StationOwners.FirstOrDefaultAsync(o => o.UserId == userId);
        if (owner == null)
        {
            return RedirectToAction(nameof(Apply));
        }

        return View("~/Views/Partner/Status.cshtml", owner);
    }
}
