using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text;
using VoltNet.Data;
using VoltNet.Models;

namespace VoltNet.Controllers.admin;

[Authorize(Roles = "SuperAdmin,Admin")]
[Route("admin/revenue")]
public class AdminRevenueController : Controller
{
    private readonly AppDbContext _context;

    public AdminRevenueController(AppDbContext context)
    {
        _context = context;
    }

    // GET: /admin/revenue
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var istNow = DateTime.UtcNow.AddHours(5).AddMinutes(30);
        var today = istNow.Date;
        var startOfCurrentMonth = new DateTime(today.Year, today.Month, 1);

        // 1. Booking Revenue & Energy Metrics Platform-Wide
        var completedBookings = await _context.Bookings
            .Include(b => b.Station)
            .Include(b => b.Charger)
            .Where(b => b.Status == "Completed")
            .ToListAsync();

        var totalBookingRevenue = completedBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        var totalBookingCount = completedBookings.Count;
        var totalEnergyKwh = completedBookings.Sum(b => b.UnitsConsumedKwh ?? 0m);

        var thisMonthBookingRevenue = completedBookings
            .Where(b => b.BookingDate >= startOfCurrentMonth)
            .Sum(b => b.ActualCost ?? b.EstimatedCost);

        // 2. Subscription Revenue & Counts
        var allSubscriptions = await _context.OwnerSubscriptions
            .Include(s => s.Plan)
            .Include(s => s.Owner)
            .ToListAsync();

        var totalSubscriptionRevenue = allSubscriptions
            .Where(s => s.Status != "Cancelled")
            .Sum(s => s.AmountPaid - (s.RefundAmount ?? 0m));

        var thisMonthSubscriptionRevenue = allSubscriptions
            .Where(s => s.Status != "Cancelled" && s.CreatedAt >= startOfCurrentMonth)
            .Sum(s => s.AmountPaid - (s.RefundAmount ?? 0m));

        var activeSubsCount = allSubscriptions.Count(s => s.Status == "Active" && s.EndDate >= DateTime.UtcNow);
        var expiredSubsCount = allSubscriptions.Count(s => s.Status == "Expired" || (s.Status == "Active" && s.EndDate < DateTime.UtcNow));
        var cancelledSubsCount = allSubscriptions.Count(s => s.Status == "Cancelled");

        // Combined Platform Gross Merchandise Value (GMV)
        var totalPlatformGMV = totalBookingRevenue + totalSubscriptionRevenue;
        var thisMonthGMV = thisMonthBookingRevenue + thisMonthSubscriptionRevenue;

        // 3. Top 10 Earning Stations
        var stationsWithBookings = completedBookings
            .Where(b => b.Station != null)
            .GroupBy(b => b.Station!)
            .Select(g => new TopStationRevenueItem
            {
                StationId = g.Key.Id,
                StationName = g.Key.Name,
                City = g.Key.City,
                Address = g.Key.Address,
                TotalBookings = g.Count(),
                TotalEnergyKwh = g.Sum(b => b.UnitsConsumedKwh ?? 0m),
                TotalRevenue = g.Sum(b => b.ActualCost ?? b.EstimatedCost)
            })
            .OrderByDescending(s => s.TotalRevenue)
            .Take(10)
            .ToList();

        // 4. Top 5 Earning Cities
        var allStations = await _context.Stations.ToListAsync();
        var cityStationCounts = allStations
            .GroupBy(s => s.City.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var topCities = completedBookings
            .Where(b => b.Station != null && !string.IsNullOrWhiteSpace(b.Station.City))
            .GroupBy(b => b.Station!.City.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new TopCityRevenueItem
            {
                City = g.Key,
                StationCount = cityStationCounts.ContainsKey(g.Key) ? cityStationCounts[g.Key] : 1,
                TotalBookings = g.Count(),
                TotalRevenue = g.Sum(b => b.ActualCost ?? b.EstimatedCost),
                TotalEnergyKwh = g.Sum(b => b.UnitsConsumedKwh ?? 0m)
            })
            .OrderByDescending(c => c.TotalRevenue)
            .Take(5)
            .ToList();

        // 5. 12-Month Trend Data for Chart.js
        var trendLabels = new List<string>();
        var trendChargingRevenue = new List<decimal>();
        var trendSubscriptionRevenue = new List<decimal>();
        var trendTotalRevenue = new List<decimal>();

        for (int i = 11; i >= 0; i--)
        {
            var mDate = today.AddMonths(-i);
            var mStart = new DateTime(mDate.Year, mDate.Month, 1);
            var mEnd = mStart.AddMonths(1).AddTicks(-1);

            trendLabels.Add(mStart.ToString("MMM yyyy"));

            var chargingRev = completedBookings
                .Where(b => b.BookingDate >= mStart && b.BookingDate <= mEnd)
                .Sum(b => b.ActualCost ?? b.EstimatedCost);

            var subRev = allSubscriptions
                .Where(s => s.Status != "Cancelled" && s.CreatedAt >= mStart && s.CreatedAt <= mEnd)
                .Sum(s => s.AmountPaid - (s.RefundAmount ?? 0m));

            trendChargingRevenue.Add(chargingRev);
            trendSubscriptionRevenue.Add(subRev);
            trendTotalRevenue.Add(chargingRev + subRev);
        }

        var vm = new AdminRevenueViewModel
        {
            TotalPlatformGMV = totalPlatformGMV,
            ThisMonthGMV = thisMonthGMV,
            TotalBookingRevenue = totalBookingRevenue,
            ThisMonthBookingRevenue = thisMonthBookingRevenue,
            TotalBookingCount = totalBookingCount,
            TotalEnergyKwh = totalEnergyKwh,

            TotalSubscriptionRevenue = totalSubscriptionRevenue,
            ThisMonthSubscriptionRevenue = thisMonthSubscriptionRevenue,
            ActiveSubscriptionsCount = activeSubsCount,
            ExpiredSubscriptionsCount = expiredSubsCount,
            CancelledSubscriptionsCount = cancelledSubsCount,

            TotalStationsCount = allStations.Count,
            TopStations = stationsWithBookings,
            TopCities = topCities,

            MonthLabelsJson = System.Text.Json.JsonSerializer.Serialize(trendLabels),
            ChargingRevenueJson = System.Text.Json.JsonSerializer.Serialize(trendChargingRevenue),
            SubscriptionRevenueJson = System.Text.Json.JsonSerializer.Serialize(trendSubscriptionRevenue),
            TotalRevenueJson = System.Text.Json.JsonSerializer.Serialize(trendTotalRevenue)
        };

        return View("~/Views/admin/revenue/Index.cshtml", vm);
    }

    // GET: /admin/revenue/export
    [HttpGet("export")]
    public async Task<IActionResult> ExportPlatformRevenueCsv()
    {
        var bookings = await _context.Bookings
            .Include(b => b.Station)
            .Include(b => b.Charger)
            .Include(b => b.Customer)
            .Where(b => b.Status == "Completed")
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.StartTime)
            .ToListAsync();

        var sb = new StringBuilder();
        // UTF-8 BOM for Excel
        sb.Append('\uFEFF');
        sb.AppendLine("Booking Reference,Station Name,City,Charger,Booking Date,Slot Time,Customer Name,Customer Email,Energy (kWh),Amount (INR),Payment Mode,Completed At");

        foreach (var b in bookings)
        {
            var amount = b.ActualCost ?? b.EstimatedCost;
            var completedAt = b.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? b.BookingDate.ToString("yyyy-MM-dd");
            var refCode = $"VN-{b.Id.ToString()[..8].ToUpper()}";
            var payMethod = string.IsNullOrWhiteSpace(b.PaymentMethod) ? "UPI / QR Code" : b.PaymentMethod;

            sb.AppendLine($"\"{refCode}\",\"{EscapeCsv(b.Station?.Name ?? "N/A")}\",\"{EscapeCsv(b.Station?.City ?? "N/A")}\",\"{EscapeCsv(b.Charger?.Name ?? "N/A")}\",\"{b.BookingDate:yyyy-MM-dd}\",\"{b.StartTime:hh\\:mm}-{b.EndTime:hh\\:mm}\",\"{EscapeCsv(b.CustomerName ?? b.Customer?.Fullname ?? "Walk-In")}\",\"{EscapeCsv(b.Customer?.Email ?? "N/A")}\",{b.UnitsConsumedKwh ?? 0m:F2},{amount:F2},\"{EscapeCsv(payMethod)}\",\"{completedAt}\"");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"VoltNet_Platform_Revenue_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        return File(bytes, "text/csv", fileName);
    }

    // GET: /admin/revenue/analytics
    [HttpGet("analytics")]
    public async Task<IActionResult> Analytics(
        DateTime? startDate,
        DateTime? endDate,
        string? groupBy,
        Guid? stationId,
        string? city,
        string? paymentMethod)
    {
        var istNow = DateTime.UtcNow.AddHours(5).AddMinutes(30);
        var today = istNow.Date;

        var start = startDate ?? today.AddDays(-29);
        var end = endDate ?? today;

        if (start > end)
        {
            var temp = start;
            start = end;
            end = temp;
        }

        // Determine default grouping if not supplied
        var daysSpan = (end - start).TotalDays;
        if (string.IsNullOrWhiteSpace(groupBy))
        {
            groupBy = daysSpan <= 31 ? "day" : (daysSpan <= 120 ? "week" : "month");
        }
        else
        {
            groupBy = groupBy.ToLowerInvariant();
            if (groupBy != "day" && groupBy != "week" && groupBy != "month")
                groupBy = "day";
        }

        // Available stations & cities for filters
        var allStations = await _context.Stations.OrderBy(s => s.Name).ToListAsync();
        var allCities = allStations.Select(s => s.City.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();

        // Base query for completed bookings
        var query = _context.Bookings
            .Include(b => b.Station)
            .Include(b => b.Charger)
            .Include(b => b.Customer)
            .Where(b => b.Status == "Completed" && b.BookingDate >= start.Date && b.BookingDate <= end.Date)
            .AsQueryable();

        if (stationId.HasValue && stationId.Value != Guid.Empty)
        {
            query = query.Where(b => b.StationId == stationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(b => b.Station != null && b.Station.City.ToLower() == city.Trim().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(paymentMethod))
        {
            if (paymentMethod == "UPI")
                query = query.Where(b => b.PaymentMethod != null && b.PaymentMethod.Contains("UPI"));
            else if (paymentMethod == "Cash")
                query = query.Where(b => b.PaymentMethod != null && b.PaymentMethod.Contains("Cash"));
            else if (paymentMethod == "Card")
                query = query.Where(b => b.PaymentMethod != null && (b.PaymentMethod.Contains("Card") || b.PaymentMethod.Contains("Advance")));
        }

        var bookings = await query.ToListAsync();

        // Time Series Bucketing
        var timeSeries = new List<AnalystTimeSeriesBucket>();

        if (groupBy == "day")
        {
            for (var dt = start.Date; dt <= end.Date; dt = dt.AddDays(1))
            {
                var bucketBookings = bookings.Where(b => b.BookingDate.Date == dt).ToList();
                var rev = bucketBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
                var kwh = bucketBookings.Sum(b => b.UnitsConsumedKwh ?? 0m);
                var count = bucketBookings.Count;
                var aov = count > 0 ? rev / count : 0m;
                var rate = kwh > 0 ? rev / kwh : 0m;

                timeSeries.Add(new AnalystTimeSeriesBucket
                {
                    Key = dt.ToString("yyyy-MM-dd"),
                    Label = dt.ToString("dd MMM yyyy"),
                    ShortLabel = dt.ToString("dd MMM"),
                    Count = count,
                    Revenue = rev,
                    EnergyKwh = kwh,
                    AverageOrderValue = aov,
                    EffectiveRatePerKwh = rate
                });
            }
        }
        else if (groupBy == "week")
        {
            // Align start to Monday of that week
            var curMonday = start.Date.AddDays(-(int)(start.Date.DayOfWeek == DayOfWeek.Sunday ? 6 : start.Date.DayOfWeek - DayOfWeek.Monday));
            while (curMonday <= end.Date)
            {
                var nextSunday = curMonday.AddDays(6);
                var bucketBookings = bookings.Where(b => b.BookingDate.Date >= curMonday && b.BookingDate.Date <= nextSunday && b.BookingDate.Date <= end.Date).ToList();
                var rev = bucketBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
                var kwh = bucketBookings.Sum(b => b.UnitsConsumedKwh ?? 0m);
                var count = bucketBookings.Count;
                var aov = count > 0 ? rev / count : 0m;
                var rate = kwh > 0 ? rev / kwh : 0m;

                timeSeries.Add(new AnalystTimeSeriesBucket
                {
                    Key = curMonday.ToString("yyyy-MM-dd"),
                    Label = $"{curMonday:dd MMM} - {nextSunday:dd MMM yyyy}",
                    ShortLabel = curMonday.ToString("dd MMM"),
                    Count = count,
                    Revenue = rev,
                    EnergyKwh = kwh,
                    AverageOrderValue = aov,
                    EffectiveRatePerKwh = rate
                });

                curMonday = curMonday.AddDays(7);
            }
        }
        else // month
        {
            var curMonth = new DateTime(start.Year, start.Month, 1);
            var endMonth = new DateTime(end.Year, end.Month, 1);

            while (curMonth <= endMonth)
            {
                var nextMonth = curMonth.AddMonths(1).AddTicks(-1);
                var bucketBookings = bookings.Where(b => b.BookingDate.Date >= curMonth && b.BookingDate.Date <= nextMonth && b.BookingDate.Date >= start.Date && b.BookingDate.Date <= end.Date).ToList();
                var rev = bucketBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
                var kwh = bucketBookings.Sum(b => b.UnitsConsumedKwh ?? 0m);
                var count = bucketBookings.Count;
                var aov = count > 0 ? rev / count : 0m;
                var rate = kwh > 0 ? rev / kwh : 0m;

                timeSeries.Add(new AnalystTimeSeriesBucket
                {
                    Key = curMonth.ToString("yyyy-MM"),
                    Label = curMonth.ToString("MMMM yyyy"),
                    ShortLabel = curMonth.ToString("MMM yy"),
                    Count = count,
                    Revenue = rev,
                    EnergyKwh = kwh,
                    AverageOrderValue = aov,
                    EffectiveRatePerKwh = rate
                });

                curMonth = curMonth.AddMonths(1);
            }
        }

        // Analyst Statistical Metrics
        var totalRev = bookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        var totalKwh = bookings.Sum(b => b.UnitsConsumedKwh ?? 0m);
        var totalCount = bookings.Count;
        var meanAov = totalCount > 0 ? totalRev / totalCount : 0m;
        var meanRate = totalKwh > 0 ? totalRev / totalKwh : 0m;

        var activeBuckets = timeSeries.Where(b => b.Revenue > 0).ToList();
        var peakBucket = timeSeries.OrderByDescending(b => b.Revenue).FirstOrDefault();
        var lowestBucket = activeBuckets.OrderBy(b => b.Revenue).FirstOrDefault();
        var averageBucketRevenue = timeSeries.Count > 0 ? totalRev / timeSeries.Count : 0m;

        // Payment Mode Split
        var paymentBreakdown = bookings
            .GroupBy(b => string.IsNullOrWhiteSpace(b.PaymentMethod) ? "UPI / QR Code" : b.PaymentMethod)
            .Select(g => new PaymentMethodStat
            {
                Method = g.Key,
                Revenue = g.Sum(b => b.ActualCost ?? b.EstimatedCost),
                Count = g.Count(),
                SharePercentage = totalRev > 0 ? Math.Round((g.Sum(b => b.ActualCost ?? b.EstimatedCost) / totalRev) * 100, 1) : 0
            })
            .OrderByDescending(p => p.Revenue)
            .ToList();

        // City Breakdown in Range
        var cityBreakdown = bookings
            .Where(b => b.Station != null && !string.IsNullOrWhiteSpace(b.Station.City))
            .GroupBy(b => b.Station!.City.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new CityAnalystStat
            {
                City = g.Key,
                Revenue = g.Sum(b => b.ActualCost ?? b.EstimatedCost),
                Count = g.Count(),
                EnergyKwh = g.Sum(b => b.UnitsConsumedKwh ?? 0m),
                SharePercentage = totalRev > 0 ? Math.Round((g.Sum(b => b.ActualCost ?? b.EstimatedCost) / totalRev) * 100, 1) : 0
            })
            .OrderByDescending(c => c.Revenue)
            .ToList();

        // JSON Payloads for Interactive Chart
        var chartLabels = timeSeries.Select(t => t.ShortLabel).ToList();
        var chartRevenue = timeSeries.Select(t => t.Revenue).ToList();
        var chartEnergy = timeSeries.Select(t => t.EnergyKwh).ToList();
        var chartCount = timeSeries.Select(t => t.Count).ToList();
        var chartAov = timeSeries.Select(t => t.AverageOrderValue).ToList();

        var vm = new RevenueAnalystStudioViewModel
        {
            StartDate = start,
            EndDate = end,
            GroupBy = groupBy,
            SelectedStationId = stationId,
            SelectedCity = city,
            SelectedPaymentMethod = paymentMethod,

            AvailableStations = allStations,
            AvailableCities = allCities,

            TotalRevenue = totalRev,
            TotalEnergyKwh = totalKwh,
            TotalBookings = totalCount,
            AverageOrderValue = meanAov,
            EffectiveRatePerKwh = meanRate,

            PeakPeriodLabel = peakBucket?.Label ?? "N/A",
            PeakPeriodRevenue = peakBucket?.Revenue ?? 0m,
            LowestPeriodLabel = lowestBucket?.Label ?? "N/A",
            LowestPeriodRevenue = lowestBucket?.Revenue ?? 0m,
            AveragePeriodRevenue = averageBucketRevenue,

            TimeSeries = timeSeries,
            PaymentBreakdown = paymentBreakdown,
            CityBreakdown = cityBreakdown,

            ChartLabelsJson = System.Text.Json.JsonSerializer.Serialize(chartLabels),
            ChartRevenueJson = System.Text.Json.JsonSerializer.Serialize(chartRevenue),
            ChartEnergyJson = System.Text.Json.JsonSerializer.Serialize(chartEnergy),
            ChartCountJson = System.Text.Json.JsonSerializer.Serialize(chartCount),
            ChartAovJson = System.Text.Json.JsonSerializer.Serialize(chartAov)
        };

        return View("~/Views/admin/revenue/Analytics.cshtml", vm);
    }

    // GET: /admin/revenue/analytics/export
    [HttpGet("analytics/export")]
    public async Task<IActionResult> ExportAnalyticsCsv(
        DateTime? startDate,
        DateTime? endDate,
        Guid? stationId,
        string? city,
        string? paymentMethod)
    {
        var istNow = DateTime.UtcNow.AddHours(5).AddMinutes(30);
        var today = istNow.Date;
        var start = startDate ?? today.AddDays(-29);
        var end = endDate ?? today;

        var query = _context.Bookings
            .Include(b => b.Station)
            .Include(b => b.Charger)
            .Include(b => b.Customer)
            .Where(b => b.Status == "Completed" && b.BookingDate >= start.Date && b.BookingDate <= end.Date)
            .AsQueryable();

        if (stationId.HasValue && stationId.Value != Guid.Empty)
        {
            query = query.Where(b => b.StationId == stationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(b => b.Station != null && b.Station.City.ToLower() == city.Trim().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(paymentMethod))
        {
            if (paymentMethod == "UPI") query = query.Where(b => b.PaymentMethod != null && b.PaymentMethod.Contains("UPI"));
            else if (paymentMethod == "Cash") query = query.Where(b => b.PaymentMethod != null && b.PaymentMethod.Contains("Cash"));
            else if (paymentMethod == "Card") query = query.Where(b => b.PaymentMethod != null && (b.PaymentMethod.Contains("Card") || b.PaymentMethod.Contains("Advance")));
        }

        var bookings = await query
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.StartTime)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.Append('\uFEFF'); // UTF-8 BOM
        sb.AppendLine("Booking Ref,Date,Time Slot,Station Name,City,Charger,Customer Name,Customer Email,Payment Mode,Energy (kWh),Rate (INR/kWh),Amount (INR),Completed At");

        foreach (var b in bookings)
        {
            var amount = b.ActualCost ?? b.EstimatedCost;
            var completedAt = b.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? b.BookingDate.ToString("yyyy-MM-dd");
            var refCode = $"VN-{b.Id.ToString()[..8].ToUpper()}";
            var payMode = string.IsNullOrWhiteSpace(b.PaymentMethod) ? "UPI / QR Code" : b.PaymentMethod;

            sb.AppendLine($"\"{refCode}\",\"{b.BookingDate:yyyy-MM-dd}\",\"{b.StartTime:hh\\:mm}-{b.EndTime:hh\\:mm}\",\"{EscapeCsv(b.Station?.Name ?? "N/A")}\",\"{EscapeCsv(b.Station?.City ?? "N/A")}\",\"{EscapeCsv(b.Charger?.Name ?? "N/A")}\",\"{EscapeCsv(b.CustomerName ?? b.Customer?.Fullname ?? "Walk-In")}\",\"{EscapeCsv(b.Customer?.Email ?? "N/A")}\",\"{EscapeCsv(payMode)}\",{b.UnitsConsumedKwh ?? 0m:F2},{b.AppliedRatePerKwh ?? 0m:F2},{amount:F2},\"{completedAt}\"");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"VoltNet_Analyst_Report_{start:yyyyMMdd}_to_{end:yyyyMMdd}.csv";
        return File(bytes, "text/csv", fileName);
    }

    private static string EscapeCsv(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace("\"", "\"\"");
    }
}

public class AdminRevenueViewModel
{
    // Platform GMV
    public decimal TotalPlatformGMV { get; set; }
    public decimal ThisMonthGMV { get; set; }

    // Charging Revenue
    public decimal TotalBookingRevenue { get; set; }
    public decimal ThisMonthBookingRevenue { get; set; }
    public int TotalBookingCount { get; set; }
    public decimal TotalEnergyKwh { get; set; }

    // Subscription Revenue
    public decimal TotalSubscriptionRevenue { get; set; }
    public decimal ThisMonthSubscriptionRevenue { get; set; }
    public int ActiveSubscriptionsCount { get; set; }
    public int ExpiredSubscriptionsCount { get; set; }
    public int CancelledSubscriptionsCount { get; set; }

    // Operational Footprint
    public int TotalStationsCount { get; set; }

    // Rankings
    public List<TopStationRevenueItem> TopStations { get; set; } = new();
    public List<TopCityRevenueItem> TopCities { get; set; } = new();

    // Chart.js 12-Month Trends
    public string MonthLabelsJson { get; set; } = "[]";
    public string ChargingRevenueJson { get; set; } = "[]";
    public string SubscriptionRevenueJson { get; set; } = "[]";
    public string TotalRevenueJson { get; set; } = "[]";
}

public class TopStationRevenueItem
{
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int TotalBookings { get; set; }
    public decimal TotalEnergyKwh { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class TopCityRevenueItem
{
    public string City { get; set; } = string.Empty;
    public int StationCount { get; set; }
    public int TotalBookings { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalEnergyKwh { get; set; }
}

public class RevenueAnalystStudioViewModel
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string GroupBy { get; set; } = "day"; // "day", "week", "month"
    public Guid? SelectedStationId { get; set; }
    public string? SelectedCity { get; set; }
    public string? SelectedPaymentMethod { get; set; }

    public List<Station> AvailableStations { get; set; } = new();
    public List<string> AvailableCities { get; set; } = new();

    // Summary Statistical Totals
    public decimal TotalRevenue { get; set; }
    public decimal TotalEnergyKwh { get; set; }
    public int TotalBookings { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal EffectiveRatePerKwh { get; set; }

    // High / Low Indicators
    public string PeakPeriodLabel { get; set; } = "N/A";
    public decimal PeakPeriodRevenue { get; set; }
    public string LowestPeriodLabel { get; set; } = "N/A";
    public decimal LowestPeriodRevenue { get; set; }
    public decimal AveragePeriodRevenue { get; set; }

    // Breakdown datasets
    public List<AnalystTimeSeriesBucket> TimeSeries { get; set; } = new();
    public List<PaymentMethodStat> PaymentBreakdown { get; set; } = new();
    public List<CityAnalystStat> CityBreakdown { get; set; } = new();

    // Chart.js JSON Payloads
    public string ChartLabelsJson { get; set; } = "[]";
    public string ChartRevenueJson { get; set; } = "[]";
    public string ChartEnergyJson { get; set; } = "[]";
    public string ChartCountJson { get; set; } = "[]";
    public string ChartAovJson { get; set; } = "[]";
}

public class AnalystTimeSeriesBucket
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ShortLabel { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Revenue { get; set; }
    public decimal EnergyKwh { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal EffectiveRatePerKwh { get; set; }
}

public class PaymentMethodStat
{
    public string Method { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int Count { get; set; }
    public decimal SharePercentage { get; set; }
}

public class CityAnalystStat
{
    public string City { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int Count { get; set; }
    public decimal EnergyKwh { get; set; }
    public decimal SharePercentage { get; set; }
}

