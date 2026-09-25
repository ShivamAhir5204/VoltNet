using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;

using Microsoft.AspNetCore.Authorization;

namespace VoltNet.Controllers.admin;

[Authorize(Roles = "SuperAdmin,Admin")]
[Route("admin/UserMaster/{action=Index}/{id?}")]
public class UserMasterController : Controller
{
    private readonly AppDbContext _context;

    public UserMasterController(AppDbContext context)
    {
        _context = context;
    }

    // GET: admin/UserMaster
    public async Task<IActionResult> Index()
    {
        var users = await _context.UserMasters
            .Where(u => u.Role != "SuperAdmin")
            .ToListAsync();
        return View("~/Views/admin/usermaster/Index.cshtml", users);
    }

    // POST: admin/UserMaster/LoadDetails
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadDetails(Guid id)
    {
        var user = await _context.UserMasters.FirstOrDefaultAsync(m => m.Id == id);
        if (user == null)
            return NotFound();

        return View("~/Views/admin/usermaster/Details.cshtml", user);
    }

    // GET: admin/UserMaster/Create
    public IActionResult Create()
    {
        return View("~/Views/admin/usermaster/Create.cshtml");
    }

    // POST: admin/UserMaster/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Fullname,Email,Password,Mobile,City,State,Role,Isactive")] UserMaster user)
    {
        if (string.IsNullOrEmpty(user.Password))
        {
            ModelState.AddModelError("Password", "Password is required.");
        }

        if (ModelState.IsValid)
        {
            user.Id = Guid.NewGuid();
            user.CreatedAt = DateTime.UtcNow;
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<UserMaster>();
            user.Password = hasher.HashPassword(user, user.Password);
            _context.Add(user);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View("~/Views/admin/usermaster/Create.cshtml", user);
    }

    // POST: admin/UserMaster/LoadEdit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadEdit(Guid id)
    {
        var user = await _context.UserMasters.FindAsync(id);
        if (user == null)
            return NotFound();

        return View("~/Views/admin/usermaster/Edit.cshtml", user);
    }

    // POST: admin/UserMaster/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [Bind("Id,Fullname,Email,Password,Mobile,City,State,Role,Isactive,CreatedAt")] UserMaster user)
    {
        if (id != user.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                var existingUser = await _context.UserMasters.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
                if (existingUser == null) return NotFound();

                if (string.IsNullOrEmpty(user.Password))
                {
                    user.Password = existingUser.Password;
                }
                else if (!user.Password.StartsWith("AQAAAA"))
                {
                    var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<UserMaster>();
                    user.Password = hasher.HashPassword(user, user.Password);
                }
                _context.Update(user);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserMasterExists(user.Id))
                    return NotFound();
                else
                    throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View("~/Views/admin/usermaster/Edit.cshtml", user);
    }


    // POST: admin/UserMaster/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var user = await _context.UserMasters.FindAsync(id);
        if (user != null)
        {
            _context.UserMasters.Remove(user);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private bool UserMasterExists(Guid id)
    {
        return _context.UserMasters.Any(e => e.Id == id);
    }
}
