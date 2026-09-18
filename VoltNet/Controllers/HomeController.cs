using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;

namespace VoltNet.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;

    public HomeController(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Stations()
    {
        return View();
    }

    public IActionResult Services()
    {
        return View();
    }

    public IActionResult Pricing()
    {
        return View();
    }

    public IActionResult Faq()
    {
        return View();
    }

    public IActionResult Contact()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult BecomePartner()
    {
        return View();
    }

    // GET: /nearby-map
    [HttpGet("nearby-map")]
    public IActionResult NearbyMap()
    {
        return View("~/Views/Home/NearbyMap.cshtml");
    }

    // GET: /nearby-map/data (JSON API — public, only Active stations)
    [HttpGet("nearby-map/data")]
    public async Task<IActionResult> NearbyMapData()
    {
        var stations = await _context.Stations
            .Where(s => s.Status == "Active" && (s.Latitude != 0 || s.Longitude != 0))
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Address,
                s.City,
                s.State,
                s.Latitude,
                s.Longitude,
                Opening = s.OpeningTime.ToString(@"hh\:mm"),
                Closing = s.ClosingTime.ToString(@"hh\:mm")
            })
            .ToListAsync();

        return Json(stations);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

