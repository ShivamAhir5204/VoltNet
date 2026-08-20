using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace VoltNet.Controllers.admin;

[Authorize(Roles = "SuperAdmin")]
[Route("admin/AdminUser/{action=Index}/{id?}")]
public class AdminUserController : Controller
{
    private readonly AppDbContext _context;

    public AdminUserController(AppDbContext context)
    {
        _context = context;
    }

    // GET: admin/AdminUser
    public async Task<IActionResult> Index()
    {
        var admins = await _context.AdminUsers
            .Where(u => u.Role != "SuperAdmin")
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        return View("~/Views/admin/adminuser/Index.cshtml", admins);
    }

    // POST: admin/AdminUser/LoadDetails
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadDetails(Guid id)
    {
        var admin = await _context.AdminUsers.FirstOrDefaultAsync(m => m.Id == id);
        if (admin == null)
            return NotFound();

        return View("~/Views/admin/adminuser/Details.cshtml", admin);
    }

    // GET: admin/AdminUser/Create
    public IActionResult Create()
    {
        return View("~/Views/admin/adminuser/Create.cshtml");
    }

    // POST: admin/AdminUser/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Email,Password,Role,Isactive")] AdminUser adminUser)
    {
        if (string.IsNullOrWhiteSpace(adminUser.Password))
        {
            ModelState.AddModelError("Password", "Password is required.");
        }

        if (string.IsNullOrWhiteSpace(adminUser.Role))
        {
            adminUser.Role = "Admin";
        }

        // Check if email already exists
        var emailExists = await _context.AdminUsers.AnyAsync(a => a.Email == adminUser.Email);
        if (emailExists)
        {
            ModelState.AddModelError("Email", "An admin with this email already exists.");
        }

        if (ModelState.IsValid)
        {
            adminUser.Id = Guid.NewGuid();
            adminUser.CreatedAt = DateTime.UtcNow;

            var hasher = new PasswordHasher<AdminUser>();
            adminUser.Password = hasher.HashPassword(adminUser, adminUser.Password);

            _context.AdminUsers.Add(adminUser);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Admin '{adminUser.Name}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/admin/adminuser/Create.cshtml", adminUser);
    }

    // POST: admin/AdminUser/LoadEdit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadEdit(Guid id)
    {
        var admin = await _context.AdminUsers.FindAsync(id);
        if (admin == null)
            return NotFound();

        return View("~/Views/admin/adminuser/Edit.cshtml", admin);
    }

    // POST: admin/AdminUser/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [Bind("Id,Name,Email,Password,Role,Isactive,CreatedAt")] AdminUser adminUser)
    {
        if (id != adminUser.Id)
        {
            return NotFound();
        }

        // Check duplicate email (excluding self)
        var emailExists = await _context.AdminUsers.AnyAsync(a => a.Email == adminUser.Email && a.Id != id);
        if (emailExists)
        {
            ModelState.AddModelError("Email", "An admin with this email already exists.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                var existingAdmin = await _context.AdminUsers.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
                if (existingAdmin == null) return NotFound();

                if (string.IsNullOrWhiteSpace(adminUser.Password))
                {
                    adminUser.Password = existingAdmin.Password;
                }
                else if (!adminUser.Password.StartsWith("AQAAAA"))
                {
                    var hasher = new PasswordHasher<AdminUser>();
                    adminUser.Password = hasher.HashPassword(adminUser, adminUser.Password);
                }

                _context.Update(adminUser);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Admin '{adminUser.Name}' updated successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.AdminUsers.Any(e => e.Id == adminUser.Id))
                    return NotFound();
                else
                    throw;
            }
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/admin/adminuser/Edit.cshtml", adminUser);
    }

    // POST: admin/AdminUser/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var admin = await _context.AdminUsers.FindAsync(id);
        if (admin != null)
        {
            // Prevent deleting own logged in account
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("AdminId");
            if (currentUserId != null && Guid.TryParse(currentUserId, out var loggedInGuid) && loggedInGuid == id)
            {
                TempData["Error"] = "You cannot delete your own logged-in account.";
                return RedirectToAction(nameof(Index));
            }

            _context.AdminUsers.Remove(admin);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Admin removed successfully.";
        }
        return RedirectToAction(nameof(Index));
    }
}
