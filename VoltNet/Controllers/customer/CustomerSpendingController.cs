using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using VoltNet.Data;
using VoltNet.Models;

namespace VoltNet.Controllers.customer;

[Authorize(Roles = "Customer")]
[Route("customer/spending")]
public class CustomerSpendingController : Controller
{
    private readonly AppDbContext _context;

    public CustomerSpendingController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue("UserId")!);

    // GET: /customer/spending
    [HttpGet("")]
    [HttpGet("/customer/analytics")]
    public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate)
    {
        var customerId = GetUserId();

        // IST Time calculations
        var istNow = DateTime.UtcNow.AddHours(5).AddMinutes(30);
        var today = istNow.Date;
        var startOfWeek = today.AddDays(-(int)(today.DayOfWeek == DayOfWeek.Sunday ? 6 : today.DayOfWeek - DayOfWeek.Monday));
        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var startOfYear = new DateTime(today.Year, 1, 1);

        // Fetch all completed bookings for this customer
        var allBookings = await _context.Bookings
            .Include(b => b.Station)
            .Include(b => b.Charger)
            .Where(b => b.CustomerId == customerId && b.Status == "Completed")
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.StartTime)
            .ToListAsync();

        // Macro Lifetime Metrics
        var totalLifetimeSpend = allBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        var totalLifetimeKwh = allBookings.Sum(b => b.UnitsConsumedKwh ?? 0m);
        var totalLifetimeSessions = allBookings.Count;

        var thisMonthSpend = allBookings.Where(b => b.BookingDate >= startOfMonth).Sum(b => b.ActualCost ?? b.EstimatedCost);
        var thisMonthKwh = allBookings.Where(b => b.BookingDate >= startOfMonth).Sum(b => b.UnitsConsumedKwh ?? 0m);
        var thisMonthSessions = allBookings.Count(b => b.BookingDate >= startOfMonth);

        var thisWeekSpend = allBookings.Where(b => b.BookingDate >= startOfWeek).Sum(b => b.ActualCost ?? b.EstimatedCost);
        var todaySpend = allBookings.Where(b => b.BookingDate == today).Sum(b => b.ActualCost ?? b.EstimatedCost);

        // Averages
        var avgCostPerSession = totalLifetimeSessions > 0 ? totalLifetimeSpend / totalLifetimeSessions : 0m;
        var avgCostPerKwh = totalLifetimeKwh > 0 ? totalLifetimeSpend / totalLifetimeKwh : 0m;

        // Green Environmental & Fuel Savings Impact
        // ICE petrol comparison: ~14.5 km/liter at ₹102/L (~₹7.03/km). EV: 1 kWh ~ 7 km.
        var equivalentPetrolCost = totalLifetimeKwh * 7m * (102m / 14.5m);
        var netFuelSavings = Math.Max(0m, equivalentPetrolCost - totalLifetimeSpend);
        var co2AvoidedKg = Math.Round(totalLifetimeKwh * 0.82m, 1);
        var equivalentTrees = Math.Round(co2AvoidedKg / 20m, 1);

        // Date Range Filtering (defaults to last 6 months)
        var filterStart = startDate ?? today.AddMonths(-5);
        var filterEnd = endDate ?? today;

        if (filterStart > filterEnd)
        {
            var temp = filterStart;
            filterStart = filterEnd;
            filterEnd = temp;
        }

        var filteredBookings = allBookings
            .Where(b => b.BookingDate >= filterStart.Date && b.BookingDate <= filterEnd.Date)
            .ToList();

        var periodSpend = filteredBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        var periodKwh = filteredBookings.Sum(b => b.UnitsConsumedKwh ?? 0m);
        var periodSessions = filteredBookings.Count;

        // 12-Month Monthly Spending Trend Chart Data
        var trendLabels = new List<string>();
        var trendSpendData = new List<decimal>();
        var trendKwhData = new List<decimal>();

        for (int i = 11; i >= 0; i--)
        {
            var mDate = today.AddMonths(-i);
            var mStart = new DateTime(mDate.Year, mDate.Month, 1);
            var mEnd = mStart.AddMonths(1).AddTicks(-1);

            trendLabels.Add(mStart.ToString("MMM yy"));

            var mBookings = allBookings.Where(b => b.BookingDate >= mStart && b.BookingDate <= mEnd).ToList();
            trendSpendData.Add(mBookings.Sum(b => b.ActualCost ?? b.EstimatedCost));
            trendKwhData.Add(mBookings.Sum(b => b.UnitsConsumedKwh ?? 0m));
        }

        // Frequented Stations Matrix
        var frequentedStations = allBookings
            .Where(b => b.Station != null)
            .GroupBy(b => b.Station!)
            .Select(g => new CustomerStationSpendingItem
            {
                StationId = g.Key.Id,
                StationName = g.Key.Name,
                City = g.Key.City,
                Address = g.Key.Address,
                VisitCount = g.Count(),
                TotalKwh = g.Sum(b => b.UnitsConsumedKwh ?? 0m),
                TotalSpent = g.Sum(b => b.ActualCost ?? b.EstimatedCost)
            })
            .OrderByDescending(s => s.VisitCount)
            .Take(5)
            .ToList();

        // Vehicle Breakdown
        var vehicleBreakdown = allBookings
            .GroupBy(b => string.IsNullOrWhiteSpace(b.VehicleModel) ? (string.IsNullOrWhiteSpace(b.VehicleNumberPlate) ? "Primary EV" : b.VehicleNumberPlate) : b.VehicleModel)
            .Select(g => new CustomerVehicleSpendingItem
            {
                VehicleName = g.Key,
                SessionCount = g.Count(),
                TotalKwh = g.Sum(b => b.UnitsConsumedKwh ?? 0m),
                TotalSpent = g.Sum(b => b.ActualCost ?? b.EstimatedCost)
            })
            .OrderByDescending(v => v.TotalSpent)
            .ToList();

        var vm = new CustomerSpendingViewModel
        {
            TotalLifetimeSpend = totalLifetimeSpend,
            TotalLifetimeKwh = totalLifetimeKwh,
            TotalLifetimeSessions = totalLifetimeSessions,

            ThisMonthSpend = thisMonthSpend,
            ThisMonthKwh = thisMonthKwh,
            ThisMonthSessions = thisMonthSessions,

            ThisWeekSpend = thisWeekSpend,
            TodaySpend = todaySpend,
            AverageCostPerSession = avgCostPerSession,
            AverageCostPerKwh = avgCostPerKwh,

            NetFuelSavings = netFuelSavings,
            Co2AvoidedKg = co2AvoidedKg,
            EquivalentTrees = equivalentTrees,

            StartDate = filterStart,
            EndDate = filterEnd,
            PeriodSpend = periodSpend,
            PeriodKwh = periodKwh,
            PeriodSessions = periodSessions,

            FrequentedStations = frequentedStations,
            VehicleBreakdown = vehicleBreakdown,
            RecentSessions = filteredBookings.Take(15).ToList(),

            TrendLabelsJson = System.Text.Json.JsonSerializer.Serialize(trendLabels),
            TrendSpendJson = System.Text.Json.JsonSerializer.Serialize(trendSpendData),
            TrendKwhJson = System.Text.Json.JsonSerializer.Serialize(trendKwhData)
        };

        return View("~/Views/customer/Spending.cshtml", vm);
    }

    // GET: /customer/spending/export
    [HttpGet("export")]
    public async Task<IActionResult> ExportPersonalStatementCsv(DateTime? startDate, DateTime? endDate)
    {
        var customerId = GetUserId();

        var query = _context.Bookings
            .Include(b => b.Station)
            .Include(b => b.Charger)
            .Where(b => b.CustomerId == customerId && b.Status == "Completed");

        if (startDate.HasValue) query = query.Where(b => b.BookingDate >= startDate.Value.Date);
        if (endDate.HasValue) query = query.Where(b => b.BookingDate <= endDate.Value.Date);

        var bookings = await query
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.StartTime)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.Append('\uFEFF'); // UTF-8 BOM for Excel
        sb.AppendLine("Booking Ref,Date,Time Slot,Station Name,City,Charger,Vehicle,Energy Added (kWh),Rate (INR/kWh),Total Amount (INR),Payment Status,Completed At");

        foreach (var b in bookings)
        {
            var amount = b.ActualCost ?? b.EstimatedCost;
            var completedAt = b.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? b.BookingDate.ToString("yyyy-MM-dd");
            var refCode = $"VN-{b.Id.ToString()[..8].ToUpper()}";
            var vehicle = string.IsNullOrWhiteSpace(b.VehicleModel) ? (b.VehicleNumberPlate ?? "EV") : $"{b.VehicleModel} ({b.VehicleNumberPlate ?? ""})";

            sb.AppendLine($"\"{refCode}\",\"{b.BookingDate:yyyy-MM-dd}\",\"{b.StartTime:hh\\:mm}-{b.EndTime:hh\\:mm}\",\"{EscapeCsv(b.Station?.Name ?? "N/A")}\",\"{EscapeCsv(b.Station?.City ?? "N/A")}\",\"{EscapeCsv(b.Charger?.Name ?? "N/A")}\",\"{EscapeCsv(vehicle)}\",{b.UnitsConsumedKwh ?? 0m:F2},{b.AppliedRatePerKwh ?? 0m:F2},{amount:F2},\"Paid\",\"{completedAt}\"");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"VoltNet_Charging_Statement_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        return File(bytes, "text/csv", fileName);
    }

    private static string EscapeCsv(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace("\"", "\"\"");
    }
}

public class CustomerSpendingViewModel
{
    // Lifetime Metrics
    public decimal TotalLifetimeSpend { get; set; }
    public decimal TotalLifetimeKwh { get; set; }
    public int TotalLifetimeSessions { get; set; }

    // Period / Month Metrics
    public decimal ThisMonthSpend { get; set; }
    public decimal ThisMonthKwh { get; set; }
    public int ThisMonthSessions { get; set; }
    public decimal ThisWeekSpend { get; set; }
    public decimal TodaySpend { get; set; }

    // Unit Economics
    public decimal AverageCostPerSession { get; set; }
    public decimal AverageCostPerKwh { get; set; }

    // Sustainability / Green Impact
    public decimal NetFuelSavings { get; set; }
    public decimal Co2AvoidedKg { get; set; }
    public decimal EquivalentTrees { get; set; }

    // Filter Window
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal PeriodSpend { get; set; }
    public decimal PeriodKwh { get; set; }
    public int PeriodSessions { get; set; }

    // Drilldowns
    public List<CustomerStationSpendingItem> FrequentedStations { get; set; } = new();
    public List<CustomerVehicleSpendingItem> VehicleBreakdown { get; set; } = new();
    public List<Booking> RecentSessions { get; set; } = new();

    // Chart.js JSON Payloads
    public string TrendLabelsJson { get; set; } = "[]";
    public string TrendSpendJson { get; set; } = "[]";
    public string TrendKwhJson { get; set; } = "[]";
}

public class CustomerStationSpendingItem
{
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int VisitCount { get; set; }
    public decimal TotalKwh { get; set; }
    public decimal TotalSpent { get; set; }
}

public class CustomerVehicleSpendingItem
{
    public string VehicleName { get; set; } = string.Empty;
    public int SessionCount { get; set; }
    public decimal TotalKwh { get; set; }
    public decimal TotalSpent { get; set; }
}
