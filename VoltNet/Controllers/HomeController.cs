using System.Diagnostics;
using System.Security.Claims;
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

    public async Task<IActionResult> Index()
    {
        var nearbyStations = await GetNearbyStationsAsync();
        ViewBag.NearbyStations = nearbyStations;
        return View();
    }

    /// <summary>
    /// Gets 4 stations nearest to the logged-in user's city/state.
    /// Falls back to any 4 active stations if user not logged in or no match found.
    /// Uses fuzzy matching (LIKE patterns) for city/state to handle typos.
    /// </summary>
    private async Task<List<object>> GetNearbyStationsAsync()
    {
        var activeStations = _context.Stations
            .Include(s => s.Chargers)
            .Include(s => s.OwnerSubscription)
            .Where(s => s.Status == "Active" && s.OwnerSubscription != null && s.OwnerSubscription.Status == "Active" && s.OwnerSubscription.EndDate >= DateTime.UtcNow);

        // Check if user is logged in
        var userIdStr = User.FindFirstValue("UserId");
        if (!string.IsNullOrEmpty(userIdStr) && Guid.TryParse(userIdStr, out var userId))
        {
            var user = await _context.UserMasters.FindAsync(userId);
            if (user != null)
            {
                var userCity = (user.City ?? "").Trim();
                var userState = (user.State ?? "").Trim();

                if (!string.IsNullOrEmpty(userCity) || !string.IsNullOrEmpty(userState))
                {
                    // Build fuzzy matching: try exact city first, then LIKE pattern
                    List<Station>? matched = null;

                    if (!string.IsNullOrEmpty(userCity))
                    {
                        // 1. Exact match (case-insensitive via EF/SQL)
                        matched = await activeStations
                            .Where(s => s.City.ToLower() == userCity.ToLower())
                            .OrderBy(s => s.Name)
                            .Take(4)
                            .Include(s => s.Chargers)
                            .ToListAsync();

                        // 2. If no exact match, try LIKE with first 3+ characters
                        if (matched.Count == 0 && userCity.Length >= 3)
                        {
                            var pattern = "%" + userCity.Substring(0, Math.Min(userCity.Length, 4)).ToLower() + "%";
                            matched = await activeStations
                                .Where(s => EF.Functions.Like(s.City.ToLower(), pattern))
                                .OrderBy(s => s.Name)
                                .Take(4)
                                .Include(s => s.Chargers)
                                .ToListAsync();
                        }
                    }

                    // 3. If still no match, try state match
                    if ((matched == null || matched.Count == 0) && !string.IsNullOrEmpty(userState))
                    {
                        matched = await activeStations
                            .Where(s => s.State.ToLower() == userState.ToLower())
                            .OrderBy(s => s.Name)
                            .Take(4)
                            .Include(s => s.Chargers)
                            .ToListAsync();
                    }

                    if (matched != null && matched.Count > 0)
                    {
                        return matched.Select(s => (object)new
                        {
                            s.Id,
                            s.Name,
                            s.Address,
                            s.City,
                            s.State,
                            ChargerCount = s.Chargers.Count,
                            Opening = s.OpeningTime.ToString(@"hh\:mm"),
                            Closing = s.ClosingTime.ToString(@"hh\:mm"),
                            IsOpen = IsStationOpen(s.OpeningTime, s.ClosingTime)
                        }).ToList();
                    }
                }
            }
        }

        // Fallback: any 4 active stations
        var fallback = await activeStations
            .OrderBy(s => s.Name)
            .Take(4)
            .Include(s => s.Chargers)
            .ToListAsync();

        return fallback.Select(s => (object)new
        {
            s.Id,
            s.Name,
            s.Address,
            s.City,
            s.State,
            ChargerCount = s.Chargers.Count,
            Opening = s.OpeningTime.ToString(@"hh\:mm"),
            Closing = s.ClosingTime.ToString(@"hh\:mm"),
            IsOpen = IsStationOpen(s.OpeningTime, s.ClosingTime)
        }).ToList();
    }

    private static bool IsStationOpen(TimeSpan opening, TimeSpan closing)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")).TimeOfDay;
        return closing > opening
            ? now >= opening && now <= closing
            : now >= opening || now <= closing; // handles overnight hours
    }

    public IActionResult About()
    {
        return View();
    }

    public async Task<IActionResult> Stations(string? q, string? city, string? connector)
    {
        var query = _context.Stations
            .Include(s => s.Chargers)
            .Include(s => s.ChargingRates)
            .Include(s => s.OwnerSubscription)
            .Where(s => s.Status == "Active" && s.OwnerSubscription != null && s.OwnerSubscription.Status == "Active" && s.OwnerSubscription.EndDate >= DateTime.UtcNow)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.ToLower().Trim();
            query = query.Where(s => 
                EF.Functions.Like(s.Name.ToLower(), $"%{search}%") ||
                EF.Functions.Like(s.Address.ToLower(), $"%{search}%") ||
                EF.Functions.Like(s.City.ToLower(), $"%{search}%"));
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(s => s.City == city);
        }

        if (!string.IsNullOrWhiteSpace(connector))
        {
            query = query.Where(s => s.Chargers.Any(c => c.ConnectorType == connector));
        }

        var results = await query.OrderBy(s => s.Name).ToListAsync();

        ViewBag.Stations = results.Select(s => (object)new
        {
            s.Id,
            s.Name,
            s.Address,
            s.City,
            s.State,
            ChargerCount = s.Chargers.Count,
            MaxPower = s.Chargers.Any() ? s.Chargers.Max(c => c.CapacityKw) : 0,
            MinRate = s.ChargingRates.Any(r => r.IsActive) ? s.ChargingRates.Where(r => r.IsActive).Min(r => r.RatePerKwh) : (decimal?)null,
            Opening = s.OpeningTime.ToString(@"hh\:mm"),
            Closing = s.ClosingTime.ToString(@"hh\:mm"),
            IsOpen = IsStationOpen(s.OpeningTime, s.ClosingTime)
        }).ToList();

        // Pass back current filter values for the form
        ViewBag.SearchQuery = q;
        ViewBag.SearchCity = city;
        ViewBag.SearchConnector = connector;

        // Get unique cities for the dropdown
        ViewBag.Cities = await _context.Stations
            .Include(s => s.OwnerSubscription)
            .Where(s => s.Status == "Active" && s.OwnerSubscription != null && s.OwnerSubscription.Status == "Active" && s.OwnerSubscription.EndDate >= DateTime.UtcNow)
            .Select(s => s.City)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        // Get unique connector types for the dropdown
        ViewBag.Connectors = await _context.Chargers
            .Include(c => c.Station!.OwnerSubscription)
            .Where(c => c.Status == "Active" && c.Station!.Status == "Active" && c.Station.OwnerSubscription != null && c.Station.OwnerSubscription.Status == "Active" && c.Station.OwnerSubscription.EndDate >= DateTime.UtcNow)
            .Select(c => c.ConnectorType)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return View();
    }

    [HttpGet("station/{id}")]
    public async Task<IActionResult> StationDetail(Guid id)
    {
        var station = await _context.Stations
            .Include(s => s.Chargers)
            .Include(s => s.ChargingRates)
            .Include(s => s.OwnerSubscription)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (station == null || station.Status != "Active" || station.OwnerSubscription == null || station.OwnerSubscription.Status != "Active" || station.OwnerSubscription.EndDate < DateTime.UtcNow)
        {
            return NotFound();
        }

        ViewBag.IsOpen = IsStationOpen(station.OpeningTime, station.ClosingTime);

        return View("~/Views/Home/StationDetail.cshtml", station);
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
            .Include(s => s.OwnerSubscription)
            .Where(s => s.Status == "Active" && s.OwnerSubscription != null && s.OwnerSubscription.Status == "Active" && s.OwnerSubscription.EndDate >= DateTime.UtcNow && (s.Latitude != 0 || s.Longitude != 0))
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

