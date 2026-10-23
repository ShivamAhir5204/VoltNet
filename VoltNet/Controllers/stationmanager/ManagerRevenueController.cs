using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using VoltNet.Data;
using VoltNet.Models;
using VoltNet.Controllers.stationowner;

namespace VoltNet.Controllers.stationmanager;

[Authorize(Roles = "StationManager")]
[Route("manager/revenue")]
public class ManagerRevenueController : Controller
{
    private readonly AppDbContext _context;

    public ManagerRevenueController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirstValue("UserId")!);
    }

    private async Task<StationManager?> GetCurrentManagerAsync()
    {
        var userId = GetUserId();
        return await _context.StationManagers
            .Include(m => m.Station)
            .ThenInclude(s => s!.Chargers)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.IsActive);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string? groupBy)
    {
        var manager = await GetCurrentManagerAsync();
        if (manager == null || manager.Station == null)
        {
            ViewBag.ErrorMessage = "No active station assigned to your manager account.";
            return View("~/Views/stationmanager/Revenue/Index.cshtml", null);
        }

        var station = manager.Station;
        var stationId = station.Id;

        // IST Time calculations
        var istNow = DateTime.UtcNow.AddHours(5).AddMinutes(30);
        var today = istNow.Date;
        var startOfWeek = today.AddDays(-(int)(today.DayOfWeek == DayOfWeek.Sunday ? 6 : today.DayOfWeek - DayOfWeek.Monday));
        var startOfMonth = new DateTime(today.Year, today.Month, 1);

        // Fetch completed bookings for this station
        var allCompletedBookings = await _context.Bookings
            .Include(b => b.Charger)
            .Include(b => b.Customer)
            .Where(b => b.StationId == stationId && b.Status == "Completed")
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.StartTime)
            .ToListAsync();

        // 1. Macro KPIs (All Time / Life of Station)
        var todayBookings = allCompletedBookings.Where(b => b.BookingDate == today).ToList();
        var todayRevenue = todayBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        var todayCount = todayBookings.Count;

        var weekBookings = allCompletedBookings.Where(b => b.BookingDate >= startOfWeek && b.BookingDate <= today).ToList();
        var weekRevenue = weekBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        var weekCount = weekBookings.Count;

        var monthBookings = allCompletedBookings.Where(b => b.BookingDate >= startOfMonth && b.BookingDate <= today).ToList();
        var monthRevenue = monthBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        var monthCount = monthBookings.Count;

        var allTimeRevenue = allCompletedBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        var allTimeCount = allCompletedBookings.Count;
        var totalEnergyKwh = allCompletedBookings.Sum(b => b.UnitsConsumedKwh ?? 0m);

        // 2. Custom Date Range Filtering for Review & Charts
        var filterStart = startDate ?? today.AddDays(-29);
        var filterEnd = endDate ?? today;

        if (filterStart > filterEnd)
        {
            var temp = filterStart;
            filterStart = filterEnd;
            filterEnd = temp;
        }

        if (string.IsNullOrWhiteSpace(groupBy))
        {
            var span = (filterEnd - filterStart).TotalDays;
            groupBy = span <= 31 ? "day" : (span <= 120 ? "week" : "month");
        }

        var periodBookings = allCompletedBookings
            .Where(b => b.BookingDate >= filterStart.Date && b.BookingDate <= filterEnd.Date)
            .ToList();

        var periodRevenue = periodBookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        var periodSessions = periodBookings.Count;
        var periodEnergyKwh = periodBookings.Sum(b => b.UnitsConsumedKwh ?? 0m);
        var periodAov = periodSessions > 0 ? periodRevenue / periodSessions : 0m;
        var periodRate = periodEnergyKwh > 0 ? periodRevenue / periodEnergyKwh : 0m;

        // Top Performing Charger in Period
        string topChargerName = "—";
        string topChargerConnector = "—";
        decimal topChargerRev = 0;
        int topChargerSharePct = 0;

        var topGroup = periodBookings
            .Where(b => b.Charger != null)
            .GroupBy(b => b.Charger!)
            .Select(g => new { Charger = g.Key, Rev = g.Sum(b => b.ActualCost ?? b.EstimatedCost) })
            .OrderByDescending(x => x.Rev)
            .FirstOrDefault();

        if (topGroup != null && periodRevenue > 0)
        {
            topChargerName = topGroup.Charger.Name;
            topChargerConnector = topGroup.Charger.ConnectorType;
            topChargerRev = topGroup.Rev;
            topChargerSharePct = (int)Math.Round((topChargerRev / periodRevenue) * 100);
        }

        // Peak Demand Hour
        string peakHourRange = "—";
        var peakGroup = periodBookings
            .GroupBy(b => b.StartTime.Hours)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (peakGroup != null)
        {
            var h = peakGroup.Key;
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

        // 3. Charger Breakdown
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

        // 5. Time-Series Bucketing
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
            RecentSessions = periodBookings.Take(50).ToList(),

            ChartLabelsJson = System.Text.Json.JsonSerializer.Serialize(timeSeriesLabels),
            ChartRevenueJson = System.Text.Json.JsonSerializer.Serialize(timeSeriesRev),
            ChartEnergyJson = System.Text.Json.JsonSerializer.Serialize(timeSeriesKwh),
            ChartCountJson = System.Text.Json.JsonSerializer.Serialize(timeSeriesCount),
            ChartAovJson = System.Text.Json.JsonSerializer.Serialize(timeSeriesAov),

            ChargerLabelsJson = System.Text.Json.JsonSerializer.Serialize(chargerLabels),
            ChargerDataJson = System.Text.Json.JsonSerializer.Serialize(chargerData)
        };

        ViewBag.PaymentBreakdown = paymentBreakdown;

        return View("~/Views/stationmanager/Revenue/Index.cshtml", vm);
    }

    // GET: /manager/revenue/export
    [HttpGet("export")]
    public async Task<IActionResult> ExportCsv(DateTime? startDate, DateTime? endDate)
    {
        var manager = await GetCurrentManagerAsync();
        if (manager == null || manager.Station == null)
        {
            return NotFound("No active station assigned to your manager account.");
        }

        var start = startDate?.Date ?? DateTime.UtcNow.AddDays(-30).Date;
        var end = endDate?.Date ?? DateTime.UtcNow.Date;

        var bookings = await _context.Bookings
            .Include(b => b.Charger)
            .Include(b => b.Customer)
            .Where(b => b.StationId == manager.StationId && b.Status == "Completed" && b.BookingDate >= start && b.BookingDate <= end)
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.StartTime)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Booking ID,Booking Date,Start Time,End Time,Charger,Connector,Vehicle No,Customer Name,Energy (kWh),Rate/kWh (INR),Total Amount (INR),Payment Method,Status");

        foreach (var b in bookings)
        {
            var id = b.Id.ToString();
            var date = b.BookingDate.ToString("yyyy-MM-dd");
            var startT = b.StartTime.ToString(@"hh\:mm");
            var endT = b.EndTime.ToString(@"hh\:mm");
            var charger = $"\"{b.Charger?.Name ?? "—"}\"";
            var connector = b.Charger?.ConnectorType ?? "—";
            var plate = b.VehicleNumberPlate ?? "—";
            var customer = $"\"{b.CustomerName ?? b.Customer?.Fullname ?? "Walk-In Driver"}\"";
            var kwh = b.UnitsConsumedKwh?.ToString("F2") ?? "0.00";
            var rate = b.AppliedRatePerKwh?.ToString("F2") ?? "0.00";
            var cost = (b.ActualCost ?? b.EstimatedCost).ToString("F2");
            var payMethod = b.PaymentMethod ?? "UPI / QR Code";
            var status = b.Status;

            sb.AppendLine($"{id},{date},{startT},{endT},{charger},{connector},{plate},{customer},{kwh},{rate},{cost},{payMethod},{status}");
        }

        var fileName = $"VoltNet_StationRevenue_{manager.Station.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
    }

    // GET: /manager/revenue/export-pdf
    [HttpGet("export-pdf")]
    public async Task<IActionResult> ExportPdf(DateTime? startDate, DateTime? endDate)
    {
        var manager = await GetCurrentManagerAsync();
        if (manager == null || manager.Station == null)
        {
            return NotFound("No active station assigned to your manager account.");
        }

        var start = startDate?.Date ?? DateTime.UtcNow.AddDays(-30).Date;
        var end = endDate?.Date ?? DateTime.UtcNow.Date;

        var bookings = await _context.Bookings
            .Include(b => b.Charger)
            .Include(b => b.Customer)
            .Where(b => b.StationId == manager.StationId && b.Status == "Completed" && b.BookingDate >= start && b.BookingDate <= end)
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.StartTime)
            .ToListAsync();

        var totalRev = bookings.Sum(b => b.ActualCost ?? b.EstimatedCost);
        var totalKwh = bookings.Sum(b => b.UnitsConsumedKwh ?? 0m);

        var pdfBytes = VoltNet.Services.PdfGeneratorService.GenerateRevenueReportPdf(
            $"{manager.Station.Name} - Station Revenue Report",
            $"Station: {manager.Station.City}, {manager.Station.State}",
            start, end, totalRev, bookings.Count, totalKwh, bookings, manager.FullName ?? "Station Manager");

        var fileName = $"VoltNet_StationRevenue_{manager.Station.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }
}
