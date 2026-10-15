using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VoltNet.Data;
using VoltNet.Models;

namespace VoltNet.Controllers.customer;

[Authorize(Roles = "Customer")]
[Route("customer/vehicles")]
public class CustomerVehicleController : Controller
{
    private readonly AppDbContext _context;

    public CustomerVehicleController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue("UserId")!);

    // GET: /customer/vehicles
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var customerId = GetUserId();
        var vehicles = await _context.Vehicles
            .Where(v => v.CustomerId == customerId)
            .OrderByDescending(v => v.IsDefault)
            .ThenByDescending(v => v.CreatedAt)
            .ToListAsync();

        return View("~/Views/customer/Vehicle/Index.cshtml", vehicles);
    }

    // GET: /customer/vehicles/create
    [HttpGet("create")]
    public IActionResult Create()
    {
        return View("~/Views/customer/Vehicle/Create.cshtml", new Vehicle());
    }

    // POST: /customer/vehicles/create
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Vehicle input)
    {
        var customerId = GetUserId();

        if (string.IsNullOrWhiteSpace(input.VehicleNumberPlate))
            ModelState.AddModelError("VehicleNumberPlate", "Vehicle Number Plate is required.");

        if (string.IsNullOrWhiteSpace(input.Brand))
            ModelState.AddModelError("Brand", "Brand is required.");

        if (string.IsNullOrWhiteSpace(input.Model))
            ModelState.AddModelError("Model", "Model is required.");

        if (string.IsNullOrWhiteSpace(input.ConnectorType))
            ModelState.AddModelError("ConnectorType", "Connector Type is required.");

        if (!ModelState.IsValid)
            return View("~/Views/customer/Vehicle/Create.cshtml", input);

        // Normalize plate
        input.VehicleNumberPlate = input.VehicleNumberPlate.Trim().ToUpperInvariant();
        input.Brand = input.Brand.Trim();
        input.Model = input.Model.Trim();
        input.ConnectorType = input.ConnectorType.Trim();
        input.CustomerId = customerId;
        input.CreatedAt = DateTime.UtcNow;

        // If this is user's first vehicle or marked default, set isDefault
        bool hasAny = await _context.Vehicles.AnyAsync(v => v.CustomerId == customerId);
        if (!hasAny || input.IsDefault)
        {
            if (input.IsDefault)
            {
                var others = await _context.Vehicles.Where(v => v.CustomerId == customerId).ToListAsync();
                foreach (var o in others) o.IsDefault = false;
            }
            else
            {
                input.IsDefault = true;
            }
        }

        _context.Vehicles.Add(input);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Vehicle '{input.VehicleNumberPlate}' ({input.Brand} {input.Model}) added successfully!";
        return RedirectToAction(nameof(Index));
    }

    // GET: /customer/vehicles/edit/{id}
    [HttpGet("edit/{id}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var customerId = GetUserId();
        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id && v.CustomerId == customerId);

        if (vehicle == null)
            return NotFound();

        return View("~/Views/customer/Vehicle/Edit.cshtml", vehicle);
    }

    // POST: /customer/vehicles/edit/{id}
    [HttpPost("edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Vehicle input)
    {
        var customerId = GetUserId();
        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id && v.CustomerId == customerId);

        if (vehicle == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(input.VehicleNumberPlate))
            ModelState.AddModelError("VehicleNumberPlate", "Vehicle Number Plate is required.");

        if (string.IsNullOrWhiteSpace(input.Brand))
            ModelState.AddModelError("Brand", "Brand is required.");

        if (string.IsNullOrWhiteSpace(input.Model))
            ModelState.AddModelError("Model", "Model is required.");

        if (!ModelState.IsValid)
            return View("~/Views/customer/Vehicle/Edit.cshtml", input);

        vehicle.VehicleNumberPlate = input.VehicleNumberPlate.Trim().ToUpperInvariant();
        vehicle.Brand = input.Brand.Trim();
        vehicle.Model = input.Model.Trim();
        vehicle.BatteryCapacityKwh = input.BatteryCapacityKwh;
        vehicle.ConnectorType = input.ConnectorType.Trim();

        if (input.IsDefault && !vehicle.IsDefault)
        {
            var others = await _context.Vehicles.Where(v => v.CustomerId == customerId && v.Id != id).ToListAsync();
            foreach (var o in others) o.IsDefault = false;
            vehicle.IsDefault = true;
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Vehicle '{vehicle.VehicleNumberPlate}' updated successfully!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /customer/vehicles/delete/{id}
    [HttpPost("delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var customerId = GetUserId();
        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id && v.CustomerId == customerId);

        if (vehicle == null)
            return NotFound();

        bool wasDefault = vehicle.IsDefault;
        _context.Vehicles.Remove(vehicle);
        await _context.SaveChangesAsync();

        // If the deleted vehicle was default, set the next one as default
        if (wasDefault)
        {
            var next = await _context.Vehicles.FirstOrDefaultAsync(v => v.CustomerId == customerId);
            if (next != null)
            {
                next.IsDefault = true;
                await _context.SaveChangesAsync();
            }
        }

        TempData["SuccessMessage"] = "Vehicle deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /customer/vehicles/set-default/{id}
    [HttpPost("set-default/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault(Guid id)
    {
        var customerId = GetUserId();
        var vehicles = await _context.Vehicles.Where(v => v.CustomerId == customerId).ToListAsync();
        var target = vehicles.FirstOrDefault(v => v.Id == id);

        if (target == null)
            return NotFound();

        foreach (var v in vehicles)
        {
            v.IsDefault = (v.Id == id);
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"'{target.VehicleNumberPlate}' is now your default vehicle.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /customer/vehicles/api
    [HttpGet("api")]
    public async Task<IActionResult> GetVehiclesJson()
    {
        var customerId = GetUserId();
        var vehicles = await _context.Vehicles
            .Where(v => v.CustomerId == customerId)
            .OrderByDescending(v => v.IsDefault)
            .Select(v => new
            {
                v.Id,
                v.VehicleNumberPlate,
                v.Brand,
                v.Model,
                v.BatteryCapacityKwh,
                v.ConnectorType,
                v.IsDefault
            })
            .ToListAsync();

        return Json(vehicles);
    }
}
