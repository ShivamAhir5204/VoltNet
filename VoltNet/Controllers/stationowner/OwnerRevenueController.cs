using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using VoltNet.Data;
using VoltNet.Models;

namespace VoltNet.Controllers.stationowner;

[Authorize(Roles = "StationOwner")]
[Route("owner/revenue")]
public class OwnerRevenueController : Controller
{
    private readonly AppDbContext _context;

    public OwnerRevenueController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue("UserId")!);

    // GET: /owner/revenue
    [HttpGet("")]
    public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, Guid? stationId, string? groupBy)
    {
        var userId = GetUserId();

        // 1. Fetch all stations owned by this user
        var ownedStations = await _context.Stations
            .Where(s => s.OwnerUserId == userId)
            .OrderBy(s => s.Name)
            .ToListAsync();

        var stationIds = ownedStations.Select(s => s.Id).ToList();

        if (!stationIds.Any())
        {
            var emptyVm = new OwnerRevenueViewModel
            {
                HasStations = false,
                OwnedStations = new List<Station>()
            };
            return View("~/Views/stationowner/Revenue/Index.cshtml", emptyVm);
        }

        // IST Time calculations
        var istNow = DateTime.UtcNow.AddHours(5).AddMinutes(30);
        var today = istNow.Date;
        var startOfWeek = today.AddDays(-(int)(today.DayOfWeek == DayOfWeek.Sunday ? 6 : today.DayOfWeek - DayOfWeek.Monday));
        var startOfMonth = new DateTime(today.Year, today.Month, 1);

        // 2. Base Query for Completed Bookings
        var baseQuery = _context.Bookings
            .Include(b => b.Station)
            .Include(b => b.Charger)
            .Where(b => stationIds.Contains(b.StationId) && b.Status == "Completed");

        // KPI metrics across all owned stations (unfiltered by custom date for macro cards)
        var todayRevenue = await baseQuery
            .Where(b => b.BookingDate == today)
            .SumAsync(b => b.ActualCost ?? b.EstimatedCost);

        var todayCount = await baseQuery
            .Where(b => b.BookingDate == today)
            .CountAsync();

        var weekRevenue = await baseQuery
            .Where(b => b.BookingDate >= startOfWeek && b.BookingDate <= today)
            .SumAsync(b => b.ActualCost ?? b.EstimatedCost);

        var weekCount = await baseQuery
            .Where(b => b.BookingDate >= startOfWeek && b.BookingDate <= today)
            .CountAsync();

        var monthRevenue = await baseQuery
            .Where(b => b.BookingDate >= startOfMonth && b.BookingDate <= today)
            .SumAsync(b => b.ActualCost ?? b.EstimatedCost);

        var monthCount = await baseQuery
            .Where(b => b.BookingDate >= startOfMonth && b.BookingDate <= today)
            .CountAsync();

        var allTimeRevenue = await baseQuery
            .SumAsync(b => b.ActualCost ?? b.EstimatedCost);

        var allTimeCount = await baseQuery
            .CountAsync();

        // 3. Filtered Query for the Station Breakdown & Analytics
        var filteredQuery = baseQuery.AsQueryable();

        if (stationId.HasValue && stationId.Value != Guid.Empty)
        {
            // Verify IDOR: ensure station belongs to owner
            if (stationIds.Contains(stationId.Value))
            {
                filteredQuery = filteredQuery.Where(b => b.StationId == stationId.Value);
            }
        }

        // Default date range: Last 30 days if not specified
        var filterStart = startDate ?? today.AddDays(-29);
        var filterEnd = endDate ?? today;

        if (filterStart > filterEnd)
        {
            var temp = filterStart;
            filterStart = filterEnd;
            filterEnd = temp;
        }

        // Granularity
        if (string.IsNullOrWhiteSpace(groupBy))
        {
            var span = (filterEnd - filterStart).TotalDays;
            groupBy = span <= 31 ? "day" : (span <= 120 ? "week" : "month");
        }
        else
        {
            groupBy = groupBy.ToLowerInvariant();
            if (groupBy != "day" && groupBy != "week" && groupBy != "month") groupBy = "day";
        }

        filteredQuery = filteredQuery.Where(b => b.BookingDate >= filterStart.Date && b.BookingDate <= filterEnd.Date);

        var filteredBookings = await filteredQuery.ToListAsync();

        var periodRevenue = filteredBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        var periodBookingsCount = filteredBookings.Count;
        var periodUnitsDispensed = filteredBookings.Sum(b => b.UnitsConsumedKwh ?? 0m);
        var periodAvgTicket = periodBookingsCount > 0 ? periodRevenue / periodBookingsCount : 0;

        // 4. Station Comparison Data
        var stationComparison = ownedStations.Select(station =>
        {
            var stBookings = filteredBookings.Where(b => b.StationId == station.Id).ToList();
            var rev = stBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
            var units = stBookings.Sum(b => b.UnitsConsumedKwh ?? 0m);
            var count = stBookings.Count;
            var share = periodRevenue > 0 ? (rev / periodRevenue) * 100 : 0;

            return new StationRevenueComparisonItem
            {
                StationId = station.Id,
                StationName = station.Name,
                City = station.City,
                Address = station.Address,
                TotalBookings = count,
                TotalRevenue = rev,
                TotalUnitsKWh = units,
                RevenueSharePercentage = Math.Round(share, 1)
            };
        }).OrderByDescending(s => s.TotalRevenue).ToList();

        // 5. Chart 1: Per-Station Revenue Bar Chart
        var stationChartLabels = stationComparison.Select(s => s.StationName).ToList();
        var stationChartData = stationComparison.Select(s => s.TotalRevenue).ToList();

        // 6. Chart 2: Grouped Trend for the Selected Period
        var trendLabels = new List<string>();
        var trendData = new List<decimal>();
        var trendEnergyData = new List<decimal>();

        if (groupBy == "day")
        {
            for (var dt = filterStart.Date; dt <= filterEnd.Date; dt = dt.AddDays(1))
            {
                trendLabels.Add(dt.ToString("dd MMM"));
                var dayBookings = filteredBookings.Where(b => b.BookingDate == dt).ToList();
                trendData.Add(dayBookings.Sum(b => b.ActualCost ?? b.EstimatedCost));
                trendEnergyData.Add(dayBookings.Sum(b => b.UnitsConsumedKwh ?? 0m));
            }
        }
        else if (groupBy == "week")
        {
            var curMonday = filterStart.Date.AddDays(-(int)(filterStart.Date.DayOfWeek == DayOfWeek.Sunday ? 6 : filterStart.Date.DayOfWeek - DayOfWeek.Monday));
            while (curMonday <= filterEnd.Date)
            {
                var nextSunday = curMonday.AddDays(6);
                trendLabels.Add(curMonday.ToString("dd MMM"));
                var weekBookings = filteredBookings.Where(b => b.BookingDate.Date >= curMonday && b.BookingDate.Date <= nextSunday && b.BookingDate.Date <= filterEnd.Date).ToList();
                trendData.Add(weekBookings.Sum(b => b.ActualCost ?? b.EstimatedCost));
                trendEnergyData.Add(weekBookings.Sum(b => b.UnitsConsumedKwh ?? 0m));
                curMonday = curMonday.AddDays(7);
            }
        }
        else // month
        {
            var curMonth = new DateTime(filterStart.Year, filterStart.Month, 1);
            var endMonth = new DateTime(filterEnd.Year, filterEnd.Month, 1);
            while (curMonth <= endMonth)
            {
                var nextMonth = curMonth.AddMonths(1).AddTicks(-1);
                trendLabels.Add(curMonth.ToString("MMM yy"));
                var monthBookings = filteredBookings.Where(b => b.BookingDate.Date >= curMonth && b.BookingDate.Date <= nextMonth && b.BookingDate.Date >= filterStart.Date && b.BookingDate.Date <= filterEnd.Date).ToList();
                trendData.Add(monthBookings.Sum(b => b.ActualCost ?? b.EstimatedCost));
                trendEnergyData.Add(monthBookings.Sum(b => b.UnitsConsumedKwh ?? 0m));
                curMonth = curMonth.AddMonths(1);
            }
        }

        var vm = new OwnerRevenueViewModel
        {
            HasStations = true,
            OwnedStations = ownedStations,
            SelectedStationId = stationId,
            StartDate = filterStart,
            EndDate = filterEnd,
            GroupBy = groupBy,

            TodayRevenue = todayRevenue,
            TodayBookings = todayCount,
            WeekRevenue = weekRevenue,
            WeekBookings = weekCount,
            MonthRevenue = monthRevenue,
            MonthBookings = monthCount,
            AllTimeRevenue = allTimeRevenue,
            AllTimeBookings = allTimeCount,

            PeriodRevenue = periodRevenue,
            PeriodBookings = periodBookingsCount,
            PeriodUnitsKWh = periodUnitsDispensed,
            PeriodAvgTicket = periodAvgTicket,

            StationComparisons = stationComparison,
            StationChartLabelsJson = System.Text.Json.JsonSerializer.Serialize(stationChartLabels),
            StationChartDataJson = System.Text.Json.JsonSerializer.Serialize(stationChartData),
            TrendChartLabelsJson = System.Text.Json.JsonSerializer.Serialize(trendLabels),
            TrendChartDataJson = System.Text.Json.JsonSerializer.Serialize(trendData)
        };

        return View("~/Views/stationowner/Revenue/Index.cshtml", vm);
    }

    // GET: /owner/revenue/export
    [HttpGet("export")]
    public async Task<IActionResult> ExportCsv(DateTime? startDate, DateTime? endDate, Guid? stationId)
    {
        var userId = GetUserId();

        var ownedStations = await _context.Stations
            .Where(s => s.OwnerUserId == userId)
            .Select(s => s.Id)
            .ToListAsync();

        if (!ownedStations.Any())
        {
            return BadRequest("No stations found for this account.");
        }

        var query = _context.Bookings
            .Include(b => b.Station)
            .Include(b => b.Charger)
            .Include(b => b.Customer)
            .Where(b => ownedStations.Contains(b.StationId) && b.Status == "Completed");

        if (stationId.HasValue && stationId.Value != Guid.Empty && ownedStations.Contains(stationId.Value))
        {
            query = query.Where(b => b.StationId == stationId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(b => b.BookingDate >= startDate.Value.Date);
        }

        if (endDate.HasValue)
        {
            query = query.Where(b => b.BookingDate <= endDate.Value.Date);
        }

        var bookings = await query
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.StartTime)
            .ToListAsync();

        // Build RFC 4180 compliant CSV with UTF-8 BOM
        var sb = new StringBuilder();
        // UTF-8 BOM
        sb.Append('\uFEFF');
        sb.AppendLine("Booking Reference,Station Name,City,Charger,Date,Slot Time,Customer Email,Energy (kWh),Amount (INR),Payment Status,Completed At");

        foreach (var b in bookings)
        {
            var amount = b.ActualCost ?? b.EstimatedCost;
            var completedAt = b.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? b.BookingDate.ToString("yyyy-MM-dd");
            var refCode = $"VN-{b.Id.ToString()[..8].ToUpper()}";

            sb.AppendLine($"\"{refCode}\",\"{EscapeCsv(b.Station?.Name ?? "N/A")}\",\"{EscapeCsv(b.Station?.City ?? "N/A")}\",\"{EscapeCsv(b.Charger?.Name ?? "N/A")}\",\"{b.BookingDate:yyyy-MM-dd}\",\"{b.StartTime:hh\\:mm}-{b.EndTime:hh\\:mm}\",\"{EscapeCsv(b.Customer?.Email ?? "Walk-In")}\",{b.UnitsConsumedKwh ?? 0m:F2},{amount:F2},\"Completed\",\"{completedAt}\"");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"VoltNet_OwnerRevenue_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        return File(bytes, "text/csv", fileName);
    }

    // GET: /owner/revenue/export-pdf
    [HttpGet("export-pdf")]
    public async Task<IActionResult> ExportPdf(DateTime? startDate, DateTime? endDate, Guid? stationId)
    {
        var userId = GetUserId();

        var ownedStations = await _context.Stations
            .Where(s => s.OwnerUserId == userId)
            .ToListAsync();

        if (!ownedStations.Any())
        {
            return BadRequest("No stations found for this account.");
        }

        var ownedStationIds = ownedStations.Select(s => s.Id).ToList();

        var query = _context.Bookings
            .Include(b => b.Station)
            .Include(b => b.Charger)
            .Include(b => b.Customer)
            .Where(b => ownedStationIds.Contains(b.StationId) && b.Status == "Completed");

        Station? targetStation = null;
        if (stationId.HasValue && stationId.Value != Guid.Empty)
        {
            targetStation = ownedStations.FirstOrDefault(s => s.Id == stationId.Value);
            if (targetStation != null)
            {
                query = query.Where(b => b.StationId == stationId.Value);
            }
        }

        var start = startDate?.Date ?? DateTime.UtcNow.AddDays(-30).Date;
        var end = endDate?.Date ?? DateTime.UtcNow.Date;

        query = query.Where(b => b.BookingDate >= start && b.BookingDate <= end);

        var bookings = await query
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.StartTime)
            .ToListAsync();

        var totalRev = bookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        var totalKwh = bookings.Sum(b => b.UnitsConsumedKwh ?? 0m);

        var title = targetStation != null ? $"{targetStation.Name} - Revenue Report" : "Consolidated Network Revenue Report";
        var subtitle = targetStation != null ? $"Location: {targetStation.City}, {targetStation.State}" : $"{ownedStations.Count} Commercial Stations";

        var ownerUser = await _context.UserMasters.FindAsync(userId);
        var pdfBytes = VoltNet.Services.PdfGeneratorService.GenerateRevenueReportPdf(
            title, subtitle, start, end, totalRev, bookings.Count, totalKwh, bookings, ownerUser?.Fullname ?? "Station Owner");

        var fileName = $"VoltNet_Revenue_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    // GET: /owner/revenue/station/{stationId}
    [HttpGet("station/{stationId}")]
    public async Task<IActionResult> StationRevenue(
        Guid stationId,
        DateTime? startDate,
        DateTime? endDate,
        string? groupBy)
    {
        var userId = GetUserId();

        // 1. Verify IDOR: Ensure station belongs to authenticated owner
        var station = await _context.Stations
            .Include(s => s.Chargers)
            .FirstOrDefaultAsync(s => s.Id == stationId && s.OwnerUserId == userId);

        if (station == null)
        {
            return NotFound("Station not found or you are not authorized to view this station's financial telemetry.");
        }

        // IST Time calculations
        var istNow = DateTime.UtcNow.AddHours(5).AddMinutes(30);
        var today = istNow.Date;
        var startOfWeek = today.AddDays(-(int)(today.DayOfWeek == DayOfWeek.Sunday ? 6 : today.DayOfWeek - DayOfWeek.Monday));
        var startOfMonth = new DateTime(today.Year, today.Month, 1);

        // Fetch all completed bookings for this station
        var allStationBookings = await _context.Bookings
            .Include(b => b.Charger)
            .Include(b => b.Customer)
            .Where(b => b.StationId == stationId && b.Status == "Completed")
            .ToListAsync();

        // Macro Cards for this station
        var todayRevenue = allStationBookings.Where(b => b.BookingDate == today).Sum(b => b.ActualCost ?? b.EstimatedCost);
        var todayCount = allStationBookings.Count(b => b.BookingDate == today);

        var weekRevenue = allStationBookings.Where(b => b.BookingDate >= startOfWeek && b.BookingDate <= today).Sum(b => b.ActualCost ?? b.EstimatedCost);
        var weekCount = allStationBookings.Count(b => b.BookingDate >= startOfWeek && b.BookingDate <= today);

        var monthRevenue = allStationBookings.Where(b => b.BookingDate >= startOfMonth && b.BookingDate <= today).Sum(b => b.ActualCost ?? b.EstimatedCost);
        var monthCount = allStationBookings.Count(b => b.BookingDate >= startOfMonth && b.BookingDate <= today);

        var allTimeRevenue = allStationBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        var allTimeCount = allStationBookings.Count;
        var totalEnergyKwh = allStationBookings.Sum(b => b.UnitsConsumedKwh ?? 0m);

        // Date Range Filtering
        var filterStart = startDate ?? today.AddDays(-29);
        var filterEnd = endDate ?? today;

        if (filterStart > filterEnd)
        {
            var temp = filterStart;
            filterStart = filterEnd;
            filterEnd = temp;
        }

        // Granularity
        if (string.IsNullOrWhiteSpace(groupBy))
        {
            var span = (filterEnd - filterStart).TotalDays;
            groupBy = span <= 31 ? "day" : (span <= 120 ? "week" : "month");
        }
        else
        {
            groupBy = groupBy.ToLowerInvariant();
            if (groupBy != "day" && groupBy != "week" && groupBy != "month") groupBy = "day";
        }

        var periodBookings = allStationBookings
            .Where(b => b.BookingDate >= filterStart.Date && b.BookingDate <= filterEnd.Date)
            .ToList();

        var periodRevenue = periodBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        var periodSessions = periodBookings.Count;
        var periodEnergyKwh = periodBookings.Sum(b => b.UnitsConsumedKwh ?? 0m);
        var periodAov = periodSessions > 0 ? periodRevenue / periodSessions : 0m;
        var periodRate = periodEnergyKwh > 0 ? periodRevenue / periodEnergyKwh : 0m;

        // 2. Operational Metrics
        // Top Charger
        string topChargerName = "N/A";
        string topChargerConnector = "N/A";
        decimal topChargerSharePct = 0;
        decimal topChargerRev = 0;

        var chargerGrouping = periodBookings
            .Where(b => b.Charger != null)
            .GroupBy(b => b.Charger!)
            .Select(g => new
            {
                Charger = g.Key,
                Revenue = g.Sum(b => b.ActualCost ?? b.EstimatedCost),
                Energy = g.Sum(b => b.UnitsConsumedKwh ?? 0m),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Revenue)
            .FirstOrDefault();

        if (chargerGrouping != null)
        {
            topChargerName = chargerGrouping.Charger.Name;
            topChargerConnector = chargerGrouping.Charger.ConnectorType ?? "Standard";
            topChargerRev = chargerGrouping.Revenue;
            topChargerSharePct = periodRevenue > 0 ? Math.Round((topChargerRev / periodRevenue) * 100, 1) : 0;
        }

        // Peak Hour
        string peakHourRange = "N/A";
        var peakHourGroup = periodBookings
            .GroupBy(b => b.StartTime.Hours)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (peakHourGroup != null)
        {
            var h = peakHourGroup.Key;
            var startAmPm = h >= 12 ? (h == 12 ? 12 : h - 12) + " PM" : (h == 0 ? 12 : h) + " AM";
            var nextH = (h + 1) % 24;
            var endAmPm = nextH >= 12 ? (nextH == 12 ? 12 : nextH - 12) + " PM" : (nextH == 0 ? 12 : nextH) + " AM";
            peakHourRange = $"{startAmPm} - {endAmPm}";
        }

        // Bay Utilization Rate
        var totalChargersCount = station.Chargers?.Count ?? 1;
        int operatingHours = 14;
        if (station.ClosingTime > station.OpeningTime)
        {
            operatingHours = (int)Math.Max(1, (station.ClosingTime - station.OpeningTime).TotalHours);
        }
        var spanDays = Math.Max(1, (int)(filterEnd - filterStart).TotalDays + 1);
        var totalAvailableSlots = Math.Max(1, totalChargersCount * operatingHours * spanDays);
        var utilizationPct = Math.Min(100, (int)Math.Round(((double)periodSessions / totalAvailableSlots) * 100));

        // 3. Charger-by-Charger Breakdown Matrix
        var chargerBreakdown = (station.Chargers ?? new List<Charger>()).Select(c =>
        {
            var cBookings = periodBookings.Where(b => b.ChargerId == c.Id).ToList();
            var rev = cBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
            var kwh = cBookings.Sum(b => b.UnitsConsumedKwh ?? 0m);
            var count = cBookings.Count;
            var share = periodRevenue > 0 ? (rev / periodRevenue) * 100 : 0;

            return new StationChargerRevenueItem
            {
                ChargerId = c.Id,
                ChargerName = c.Name,
                ConnectorType = c.ConnectorType ?? "Standard",
                CapacityKw = c.CapacityKw,
                TotalBookings = count,
                TotalEnergyKwh = kwh,
                TotalRevenue = rev,
                RevenueSharePercentage = Math.Round(share, 1)
            };
        }).OrderByDescending(c => c.TotalRevenue).ToList();

        // 4. Payment Breakdown
        var paymentBreakdown = periodBookings
            .GroupBy(b => string.IsNullOrWhiteSpace(b.PaymentMethod) ? "UPI / QR Code" : b.PaymentMethod)
            .Select(g => new
            {
                Method = g.Key,
                Amount = g.Sum(b => b.ActualCost ?? b.EstimatedCost),
                Count = g.Count(),
                Share = periodRevenue > 0 ? Math.Round((g.Sum(b => b.ActualCost ?? b.EstimatedCost) / periodRevenue) * 100, 1) : 0
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        // 5. Time-Series Bucketing (Day / Week / Month)
        var timeSeriesLabels = new List<string>();
        var timeSeriesRev = new List<decimal>();
        var timeSeriesKwh = new List<decimal>();
        var timeSeriesCount = new List<int>();
        var timeSeriesAov = new List<decimal>();

        if (groupBy == "day")
        {
            for (var dt = filterStart.Date; dt <= filterEnd.Date; dt = dt.AddDays(1))
            {
                timeSeriesLabels.Add(dt.ToString("dd MMM"));
                var bDay = periodBookings.Where(b => b.BookingDate == dt).ToList();
                var rev = bDay.Sum(b => b.ActualCost ?? b.EstimatedCost);
                var kwh = bDay.Sum(b => b.UnitsConsumedKwh ?? 0m);
                var count = bDay.Count;

                timeSeriesRev.Add(rev);
                timeSeriesKwh.Add(kwh);
                timeSeriesCount.Add(count);
                timeSeriesAov.Add(count > 0 ? rev / count : 0m);
            }
        }
        else if (groupBy == "week")
        {
            var curMonday = filterStart.Date.AddDays(-(int)(filterStart.Date.DayOfWeek == DayOfWeek.Sunday ? 6 : filterStart.Date.DayOfWeek - DayOfWeek.Monday));
            while (curMonday <= filterEnd.Date)
            {
                var nextSunday = curMonday.AddDays(6);
                timeSeriesLabels.Add(curMonday.ToString("dd MMM"));
                var bWeek = periodBookings.Where(b => b.BookingDate.Date >= curMonday && b.BookingDate.Date <= nextSunday && b.BookingDate.Date <= filterEnd.Date).ToList();
                var rev = bWeek.Sum(b => b.ActualCost ?? b.EstimatedCost);
                var kwh = bWeek.Sum(b => b.UnitsConsumedKwh ?? 0m);
                var count = bWeek.Count;

                timeSeriesRev.Add(rev);
                timeSeriesKwh.Add(kwh);
                timeSeriesCount.Add(count);
                timeSeriesAov.Add(count > 0 ? rev / count : 0m);
                curMonday = curMonday.AddDays(7);
            }
        }
        else // month
        {
            var curMonth = new DateTime(filterStart.Year, filterStart.Month, 1);
            var endMonth = new DateTime(filterEnd.Year, filterEnd.Month, 1);
            while (curMonth <= endMonth)
            {
                var nextMonth = curMonth.AddMonths(1).AddTicks(-1);
                timeSeriesLabels.Add(curMonth.ToString("MMM yy"));
                var bMonth = periodBookings.Where(b => b.BookingDate.Date >= curMonth && b.BookingDate.Date <= nextMonth && b.BookingDate.Date >= filterStart.Date && b.BookingDate.Date <= filterEnd.Date).ToList();
                var rev = bMonth.Sum(b => b.ActualCost ?? b.EstimatedCost);
                var kwh = bMonth.Sum(b => b.UnitsConsumedKwh ?? 0m);
                var count = bMonth.Count;

                timeSeriesRev.Add(rev);
                timeSeriesKwh.Add(kwh);
                timeSeriesCount.Add(count);
                timeSeriesAov.Add(count > 0 ? rev / count : 0m);
                curMonth = curMonth.AddMonths(1);
            }
        }

        // Charger Chart Payloads
        var chargerLabels = chargerBreakdown.Select(c => c.ChargerName).ToList();
        var chargerData = chargerBreakdown.Select(c => c.TotalRevenue).ToList();

        var vm = new OwnerIndividualStationRevenueViewModel
        {
            Station = station,
            StartDate = filterStart,
            EndDate = filterEnd,
            GroupBy = groupBy,

            TodayRevenue = todayRevenue,
            TodayBookings = todayCount,
            WeekRevenue = weekRevenue,
            WeekBookings = weekCount,
            MonthRevenue = monthRevenue,
            MonthBookings = monthCount,
            AllTimeRevenue = allTimeRevenue,
            AllTimeBookings = allTimeCount,
            TotalEnergyKwh = totalEnergyKwh,

            PeriodRevenue = periodRevenue,
            PeriodBookings = periodSessions,
            PeriodEnergyKwh = periodEnergyKwh,
            PeriodAvgTicket = periodAov,
            PeriodRatePerKwh = periodRate,

            TopChargerName = topChargerName,
            TopChargerConnector = topChargerConnector,
            TopChargerSharePct = topChargerSharePct,
            TopChargerRevenue = topChargerRev,
            PeakHourRange = peakHourRange,
            UtilizationPct = utilizationPct,

            ChargerBreakdowns = chargerBreakdown,
            RecentSessions = periodBookings.OrderByDescending(b => b.BookingDate).ThenByDescending(b => b.StartTime).Take(15).ToList(),

            ChartLabelsJson = System.Text.Json.JsonSerializer.Serialize(timeSeriesLabels),
            ChartRevenueJson = System.Text.Json.JsonSerializer.Serialize(timeSeriesRev),
            ChartEnergyJson = System.Text.Json.JsonSerializer.Serialize(timeSeriesKwh),
            ChartCountJson = System.Text.Json.JsonSerializer.Serialize(timeSeriesCount),
            ChartAovJson = System.Text.Json.JsonSerializer.Serialize(timeSeriesAov),

            ChargerLabelsJson = System.Text.Json.JsonSerializer.Serialize(chargerLabels),
            ChargerDataJson = System.Text.Json.JsonSerializer.Serialize(chargerData)
        };

        ViewBag.PaymentBreakdown = paymentBreakdown;

        return View("~/Views/stationowner/Revenue/StationRevenue.cshtml", vm);
    }

    // GET: /owner/revenue/station/{stationId}/export
    [HttpGet("station/{stationId}/export")]
    public async Task<IActionResult> ExportStationCsv(Guid stationId, DateTime? startDate, DateTime? endDate)
    {
        var userId = GetUserId();

        var station = await _context.Stations
            .FirstOrDefaultAsync(s => s.Id == stationId && s.OwnerUserId == userId);

        if (station == null)
        {
            return NotFound("Station not found or you are not authorized to export this station's data.");
        }

        var query = _context.Bookings
            .Include(b => b.Charger)
            .Include(b => b.Customer)
            .Where(b => b.StationId == stationId && b.Status == "Completed");

        if (startDate.HasValue) query = query.Where(b => b.BookingDate >= startDate.Value.Date);
        if (endDate.HasValue) query = query.Where(b => b.BookingDate <= endDate.Value.Date);

        var bookings = await query
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.StartTime)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.Append('\uFEFF'); // UTF-8 BOM
        sb.AppendLine("Booking Ref,Station Name,City,Charger,Date,Slot Time,Vehicle,Customer Name,Customer Email,Payment Mode,Energy (kWh),Amount (INR),Completed At");

        foreach (var b in bookings)
        {
            var amount = b.ActualCost ?? b.EstimatedCost;
            var completedAt = b.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? b.BookingDate.ToString("yyyy-MM-dd");
            var refCode = $"VN-{b.Id.ToString()[..8].ToUpper()}";
            var vehicle = string.IsNullOrWhiteSpace(b.VehicleModel) ? (b.VehicleNumberPlate ?? "EV") : $"{b.VehicleModel} ({b.VehicleNumberPlate ?? ""})";
            var payMethod = string.IsNullOrWhiteSpace(b.PaymentMethod) ? "UPI / QR Code" : b.PaymentMethod;

            sb.AppendLine($"\"{refCode}\",\"{EscapeCsv(station.Name)}\",\"{EscapeCsv(station.City)}\",\"{EscapeCsv(b.Charger?.Name ?? "N/A")}\",\"{b.BookingDate:yyyy-MM-dd}\",\"{b.StartTime:hh\\:mm}-{b.EndTime:hh\\:mm}\",\"{EscapeCsv(vehicle)}\",\"{EscapeCsv(b.CustomerName ?? b.Customer?.Fullname ?? "Walk-In")}\",\"{EscapeCsv(b.Customer?.Email ?? "N/A")}\",\"{EscapeCsv(payMethod)}\",{b.UnitsConsumedKwh ?? 0m:F2},{amount:F2},\"{completedAt}\"");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"VoltNet_StationRevenue_{station.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        return File(bytes, "text/csv", fileName);
    }

    // GET: /owner/revenue/station/{stationId}/export-pdf
    [HttpGet("station/{stationId}/export-pdf")]
    public async Task<IActionResult> ExportStationPdf(Guid stationId, DateTime? startDate, DateTime? endDate)
    {
        var userId = GetUserId();

        var station = await _context.Stations
            .FirstOrDefaultAsync(s => s.Id == stationId && s.OwnerUserId == userId);

        if (station == null)
        {
            return NotFound("Station not found or you are not authorized to export this station's data.");
        }

        var start = startDate?.Date ?? DateTime.UtcNow.AddDays(-30).Date;
        var end = endDate?.Date ?? DateTime.UtcNow.Date;

        var bookings = await _context.Bookings
            .Include(b => b.Charger)
            .Include(b => b.Customer)
            .Where(b => b.StationId == stationId && b.Status == "Completed" && b.BookingDate >= start && b.BookingDate <= end)
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.StartTime)
            .ToListAsync();

        var totalRev = bookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        var totalKwh = bookings.Sum(b => b.UnitsConsumedKwh ?? 0m);

        var ownerUser = await _context.UserMasters.FindAsync(userId);
        var pdfBytes = VoltNet.Services.PdfGeneratorService.GenerateRevenueReportPdf(
            $"{station.Name} - Revenue Report",
            $"Location: {station.City}, {station.State}",
            start, end, totalRev, bookings.Count, totalKwh, bookings, ownerUser?.Fullname ?? "Station Owner");

        var fileName = $"VoltNet_{station.Name.Replace(" ", "_")}_Revenue_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    private static string EscapeCsv(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace("\"", "\"\"");
    }
}

public class OwnerRevenueViewModel
{
    public bool HasStations { get; set; } = true;
    public List<Station> OwnedStations { get; set; } = new();
    public Guid? SelectedStationId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string GroupBy { get; set; } = "day";

    // Macro Cards
    public decimal TodayRevenue { get; set; }
    public int TodayBookings { get; set; }
    public decimal WeekRevenue { get; set; }
    public int WeekBookings { get; set; }
    public decimal MonthRevenue { get; set; }
    public int MonthBookings { get; set; }
    public decimal AllTimeRevenue { get; set; }
    public int AllTimeBookings { get; set; }

    // Period Summary
    public decimal PeriodRevenue { get; set; }
    public int PeriodBookings { get; set; }
    public decimal PeriodUnitsKWh { get; set; }
    public decimal PeriodAvgTicket { get; set; }

    // Station Comparison
    public List<StationRevenueComparisonItem> StationComparisons { get; set; } = new();

    // Chart.js JSON Payloads
    public string StationChartLabelsJson { get; set; } = "[]";
    public string StationChartDataJson { get; set; } = "[]";
    public string TrendChartLabelsJson { get; set; } = "[]";
    public string TrendChartDataJson { get; set; } = "[]";
}

public class StationRevenueComparisonItem
{
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int TotalBookings { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalUnitsKWh { get; set; }
    public decimal RevenueSharePercentage { get; set; }
}

public class OwnerIndividualStationRevenueViewModel
{
    public Station Station { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string GroupBy { get; set; } = "day";

    // Macro Cards
    public decimal TodayRevenue { get; set; }
    public int TodayBookings { get; set; }
    public decimal WeekRevenue { get; set; }
    public int WeekBookings { get; set; }
    public decimal MonthRevenue { get; set; }
    public int MonthBookings { get; set; }
    public decimal AllTimeRevenue { get; set; }
    public int AllTimeBookings { get; set; }
    public decimal TotalEnergyKwh { get; set; }

    // Period Summary
    public decimal PeriodRevenue { get; set; }
    public int PeriodBookings { get; set; }
    public decimal PeriodEnergyKwh { get; set; }
    public decimal PeriodAvgTicket { get; set; }
    public decimal PeriodRatePerKwh { get; set; }

    // Operational Analytics
    public string TopChargerName { get; set; } = "N/A";
    public string TopChargerConnector { get; set; } = "N/A";
    public decimal TopChargerSharePct { get; set; }
    public decimal TopChargerRevenue { get; set; }
    public string PeakHourRange { get; set; } = "N/A";
    public int UtilizationPct { get; set; }

    // Charger Breakdown
    public List<StationChargerRevenueItem> ChargerBreakdowns { get; set; } = new();
    public List<Booking> RecentSessions { get; set; } = new();

    // Chart.js JSON Payloads
    public string ChartLabelsJson { get; set; } = "[]";
    public string ChartRevenueJson { get; set; } = "[]";
    public string ChartEnergyJson { get; set; } = "[]";
    public string ChartCountJson { get; set; } = "[]";
    public string ChartAovJson { get; set; } = "[]";

    public string ChargerLabelsJson { get; set; } = "[]";
    public string ChargerDataJson { get; set; } = "[]";
}

public class StationChargerRevenueItem
{
    public Guid ChargerId { get; set; }
    public string ChargerName { get; set; } = string.Empty;
    public string ConnectorType { get; set; } = string.Empty;
    public double CapacityKw { get; set; }
    public int TotalBookings { get; set; }
    public decimal TotalEnergyKwh { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal RevenueSharePercentage { get; set; }
}

