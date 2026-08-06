using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using Microsoft.AspNetCore.Authorization;

namespace VoltNet.Controllers.admin;

[Authorize(Roles = "SuperAdmin")]
[Route("admin/FranchiseMaster/{action=Index}/{id?}")]
public class FranchiseMasterController : Controller
{
    private readonly AppDbContext _context;

    public FranchiseMasterController(AppDbContext context)
    {
        _context = context;
    }

    // GET: admin/FranchiseMaster
    public async Task<IActionResult> Index()
    {
        var franchises = await _context.FranchiseMasters
            .Include(f => f.OwnerUser)
            .ToListAsync();
        return View("~/Views/admin/franchisemaster/Index.cshtml", franchises);
    }

    // POST: admin/FranchiseMaster/LoadDetails
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadDetails(Guid id)
    {
        var franchise = await _context.FranchiseMasters
            .Include(f => f.OwnerUser)
            .FirstOrDefaultAsync(f => f.Id == id);
        if (franchise == null)
            return NotFound();

        return View("~/Views/admin/franchisemaster/Details.cshtml", franchise);
    }

    // GET: admin/FranchiseMaster/Create
    public async Task<IActionResult> Create()
    {
        await PopulateFranchiseOwnersDropdown();
        return View("~/Views/admin/franchisemaster/Create.cshtml");
    }

    // POST: admin/FranchiseMaster/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,OwnerUserId,FranchiseName,RevenueSharePercent,ContactEmail,Address")] FranchiseMaster franchise)
    {
        // Validate that OwnerUserId belongs to a FranchiseOwner
        var ownerUser = await _context.UserMasters.FirstOrDefaultAsync(u => u.Id == franchise.OwnerUserId);
        if (ownerUser == null || ownerUser.Role != "FranchiseOwner")
        {
            ModelState.AddModelError("OwnerUserId", "Selected user must have the FranchiseOwner role.");
        }

        if (ModelState.IsValid)
        {
            franchise.Id = Guid.NewGuid();
            franchise.CreatedAt = DateTime.UtcNow;
            _context.Add(franchise);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        await PopulateFranchiseOwnersDropdown(franchise.OwnerUserId);
        return View("~/Views/admin/franchisemaster/Create.cshtml", franchise);
    }

    // POST: admin/FranchiseMaster/LoadEdit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadEdit(Guid id)
    {
        var franchise = await _context.FranchiseMasters.FindAsync(id);
        if (franchise == null)
            return NotFound();

        await PopulateFranchiseOwnersDropdown(franchise.OwnerUserId);
        return View("~/Views/admin/franchisemaster/Edit.cshtml", franchise);
    }

    // POST: admin/FranchiseMaster/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [Bind("Id,OwnerUserId,FranchiseName,RevenueSharePercent,ContactEmail,Address,CreatedAt")] FranchiseMaster franchise)
    {
        if (id != franchise.Id)
        {
            return NotFound();
        }

        // Validate that OwnerUserId belongs to a FranchiseOwner
        var ownerUser = await _context.UserMasters.FirstOrDefaultAsync(u => u.Id == franchise.OwnerUserId);
        if (ownerUser == null || ownerUser.Role != "FranchiseOwner")
        {
            ModelState.AddModelError("OwnerUserId", "Selected user must have the FranchiseOwner role.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(franchise);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!FranchiseMasterExists(franchise.Id))
                    return NotFound();
                else
                    throw;
            }
            return RedirectToAction(nameof(Index));
        }
        await PopulateFranchiseOwnersDropdown(franchise.OwnerUserId);
        return View("~/Views/admin/franchisemaster/Edit.cshtml", franchise);
    }

    // POST: admin/FranchiseMaster/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var franchise = await _context.FranchiseMasters.FindAsync(id);
        if (franchise != null)
        {
            _context.FranchiseMasters.Remove(franchise);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private bool FranchiseMasterExists(Guid id)
    {
        return _context.FranchiseMasters.Any(e => e.Id == id);
    }

    /// <summary>
    /// Populates ViewBag with only FranchiseOwner users for the dropdown
    /// </summary>
    private async Task PopulateFranchiseOwnersDropdown(Guid? selectedId = null)
    {
        var franchiseOwners = await _context.UserMasters
            .Where(u => u.Role == "FranchiseOwner" && u.Isactive)
            .OrderBy(u => u.Fullname)
            .Select(u => new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = u.Fullname + " (" + u.Email + ")",
                Selected = selectedId.HasValue && u.Id == selectedId.Value
            })
            .ToListAsync();

        ViewBag.FranchiseOwners = franchiseOwners;
    }
}
