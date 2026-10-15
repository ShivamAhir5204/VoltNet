using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VoltNet.Data;
using VoltNet.Models;

namespace VoltNet.Controllers.customer;

[Authorize(Roles = "Customer")]
[Route("customer/book")]
public class CustomerBookingController : Controller
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public CustomerBookingController(AppDbContext context, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue("UserId")!);

    // ── STEP 1: Choose Charger ─────────────────────────────────────────
    // GET: /customer/book/{stationId}
    [HttpGet("{stationId}")]
    public async Task<IActionResult> SelectCharger(Guid stationId)
    {
        var station = await _context.Stations
            .Include(s => s.Chargers)
            .Include(s => s.ChargingRates)
            .Include(s => s.OwnerSubscription)
            .FirstOrDefaultAsync(s => s.Id == stationId);

        if (station == null || station.Status != "Active" || station.OwnerSubscription == null ||
            station.OwnerSubscription.Status != "Active" || station.OwnerSubscription.EndDate < DateTime.UtcNow)
        {
            TempData["Error"] = "This station is currently unavailable for booking.";
            return RedirectToAction("Stations", "Home");
        }

        var activeChargers = station.Chargers.Where(c => c.Status == "Active").ToList();
        if (!activeChargers.Any())
        {
            TempData["Error"] = "No active chargers are available at this station.";
            return Redirect($"/Home/StationDetail/{stationId}");
        }

        ViewBag.Station = station;
        ViewBag.Rates = station.ChargingRates.Where(r => r.IsActive).ToList();
        return View("~/Views/customer/Booking/SelectCharger.cshtml", activeChargers);
    }

    // ── STEP 2: Choose Slot & Real-Time Queue/Waitlist ──────────────────
    // GET: /customer/book/{stationId}/slots?chargerId=xxx&date=YYYY-MM-DD
    [HttpGet("{stationId}/slots")]
    public async Task<IActionResult> SelectSlot(Guid stationId, Guid chargerId, DateTime? date)
    {
        var station = await _context.Stations
            .Include(s => s.ChargingRates)
            .Include(s => s.OwnerSubscription)
            .FirstOrDefaultAsync(s => s.Id == stationId);

        if (station == null || station.Status != "Active" || station.OwnerSubscription == null ||
            station.OwnerSubscription.Status != "Active" || station.OwnerSubscription.EndDate < DateTime.UtcNow)
        {
            TempData["Error"] = "This station is currently unavailable.";
            return RedirectToAction("Stations", "Home");
        }

        var charger = await _context.Chargers.FirstOrDefaultAsync(c => c.Id == chargerId && c.StationId == stationId);
        if (charger == null || charger.Status != "Active")
        {
            TempData["Error"] = "The selected charger is currently unavailable.";
            return RedirectToAction(nameof(SelectCharger), new { stationId });
        }

        var indiaTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
        var selectedDate = (date ?? indiaTime.Date).Date;

        if (selectedDate < indiaTime.Date || selectedDate > indiaTime.Date.AddDays(7))
        {
            selectedDate = indiaTime.Date;
        }

        // Generate 1-hour interval slots based on operating hours
        var slots = GenerateStationSlots(station.OpeningTime, station.ClosingTime, 60);

        // Fetch existing bookings (excluding cancelled)
        var bookedSlots = await _context.Bookings
            .Where(b => b.ChargerId == chargerId && b.BookingDate == selectedDate && b.Status != "Cancelled")
            .Select(b => new { b.StartTime, b.EndTime })
            .ToListAsync();

        // Fetch manager maintenance blocks for this station/charger
        var blockedSlots = await _context.StationBlockSlots
            .Where(s => s.StationId == stationId && (s.ChargerId == null || s.ChargerId == chargerId) && s.BlockDate == selectedDate)
            .ToListAsync();

        // Fetch waitlist entries for this charger on this date
        var waitlistEntries = await _context.WaitlistEntries
            .Where(w => w.ChargerId == chargerId && w.BookingDate == selectedDate && w.Status == "Waiting")
            .ToListAsync();

        var userId = GetUserId();

        var slotViewModels = slots.Select(slot =>
        {
            bool isBooked = bookedSlots.Any(b => b.StartTime < slot.EndTime && b.EndTime > slot.StartTime);
            bool isBlockedByManager = blockedSlots.Any(b => b.StartTime < slot.EndTime && b.EndTime > slot.StartTime);
            var blockReason = blockedSlots.FirstOrDefault(b => b.StartTime < slot.EndTime && b.EndTime > slot.StartTime)?.Reason;

            bool isPastTime = (selectedDate == indiaTime.Date) && (slot.StartTime <= indiaTime.TimeOfDay);

            var waitlistCount = waitlistEntries.Count(w => w.StartTime == slot.StartTime);
            var userInWaitlist = waitlistEntries.Any(w => w.StartTime == slot.StartTime && w.CustomerId == userId);

            return new SlotViewModel
            {
                StartTime = slot.StartTime,
                EndTime = slot.EndTime,
                IsAvailable = !isBooked && !isBlockedByManager && !isPastTime,
                IsBooked = isBooked,
                IsBlocked = isBlockedByManager,
                BlockReason = blockReason,
                IsPast = isPastTime,
                WaitlistCount = waitlistCount,
                IsUserInWaitlist = userInWaitlist
            };
        }).ToList();

        var rate = station.ChargingRates.FirstOrDefault(r => r.ConnectorType == charger.ConnectorType && r.IsActive);
        decimal estimatedCost = 0;
        if (rate != null)
        {
            estimatedCost = ((decimal)charger.CapacityKw * rate.RatePerKwh) + (rate.RatePerHour ?? 0);
        }

        ViewBag.Station = station;
        ViewBag.Charger = charger;
        ViewBag.SelectedDate = selectedDate;
        ViewBag.MinDate = indiaTime.Date.ToString("yyyy-MM-dd");
        ViewBag.MaxDate = indiaTime.Date.AddDays(7).ToString("yyyy-MM-dd");
        ViewBag.EstimatedCost = estimatedCost;
        ViewBag.Rate = rate;

        return View("~/Views/customer/Booking/SelectSlot.cshtml", slotViewModels);
    }

    // ── STEP 3: Review & Confirm Booking ───────────────────────────────
    [HttpGet("confirm")]
    public async Task<IActionResult> Confirm(Guid stationId, Guid chargerId, DateTime date, TimeSpan startTime, TimeSpan endTime)
    {
        var station = await _context.Stations
            .Include(s => s.ChargingRates)
            .Include(s => s.OwnerSubscription)
            .FirstOrDefaultAsync(s => s.Id == stationId);

        if (station == null || station.Status != "Active" || station.OwnerSubscription == null ||
            station.OwnerSubscription.Status != "Active" || station.OwnerSubscription.EndDate < DateTime.UtcNow)
        {
            TempData["Error"] = "This station is unavailable.";
            return RedirectToAction("Stations", "Home");
        }

        var charger = await _context.Chargers.FirstOrDefaultAsync(c => c.Id == chargerId && c.StationId == stationId);
        if (charger == null || charger.Status != "Active")
        {
            TempData["Error"] = "Charger is offline or invalid.";
            return RedirectToAction(nameof(SelectCharger), new { stationId });
        }

        var rate = station.ChargingRates.FirstOrDefault(r => r.ConnectorType == charger.ConnectorType && r.IsActive);
        decimal estimatedCost = 0;
        if (rate != null)
        {
            estimatedCost = ((decimal)charger.CapacityKw * rate.RatePerKwh) + (rate.RatePerHour ?? 0);
        }

        ViewBag.Station = station;
        ViewBag.Charger = charger;
        ViewBag.BookingDate = date;
        ViewBag.StartTime = startTime;
        ViewBag.EndTime = endTime;
        ViewBag.EstimatedCost = estimatedCost;
        ViewBag.Rate = rate;

        return View("~/Views/customer/Booking/Confirm.cshtml");
    }

    // ── POST: Create Booking ───────────────────────────────────────────
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid stationId, Guid chargerId, DateTime bookingDate, TimeSpan startTime, TimeSpan endTime, string? vehicleNumberPlate, string? vehicleModel)
    {
        var customerId = GetUserId();

        var station = await _context.Stations
            .Include(s => s.ChargingRates)
            .Include(s => s.OwnerSubscription)
            .FirstOrDefaultAsync(s => s.Id == stationId);

        if (station == null || station.Status != "Active" || station.OwnerSubscription == null ||
            station.OwnerSubscription.Status != "Active" || station.OwnerSubscription.EndDate < DateTime.UtcNow)
        {
            TempData["Error"] = "Station is not active.";
            return RedirectToAction("Stations", "Home");
        }

        var charger = await _context.Chargers.FirstOrDefaultAsync(c => c.Id == chargerId && c.StationId == stationId);
        if (charger == null || charger.Status != "Active")
        {
            TempData["Error"] = "The selected charger is not active.";
            return RedirectToAction(nameof(SelectCharger), new { stationId });
        }

        var indiaTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
        var dateOnly = bookingDate.Date;

        if (dateOnly < indiaTime.Date || dateOnly > indiaTime.Date.AddDays(7))
        {
            TempData["Error"] = "Booking date must be within the next 7 days.";
            return RedirectToAction(nameof(SelectSlot), new { stationId, chargerId });
        }

        // Overlap & Race Condition Check
        bool isSlotConflict = await _context.Bookings.AnyAsync(b =>
            b.ChargerId == chargerId &&
            b.BookingDate == dateOnly &&
            b.Status != "Cancelled" &&
            b.StartTime < endTime &&
            b.EndTime > startTime);

        bool isBlockConflict = await _context.StationBlockSlots.AnyAsync(s =>
            s.StationId == stationId &&
            (s.ChargerId == null || s.ChargerId == chargerId) &&
            s.BlockDate == dateOnly &&
            s.StartTime < endTime &&
            s.EndTime > startTime);

        if (isSlotConflict || isBlockConflict)
        {
            TempData["Error"] = "Sorry, this slot was just taken or is undergoing station maintenance. Please choose another slot or join the waitlist!";
            return RedirectToAction(nameof(SelectSlot), new { stationId, chargerId, date = dateOnly.ToString("yyyy-MM-dd") });
        }

        // User duplicate booking check for same timeslot
        bool userConflict = await _context.Bookings.AnyAsync(b =>
            b.CustomerId == customerId &&
            b.BookingDate == dateOnly &&
            b.Status != "Cancelled" &&
            b.StartTime < endTime &&
            b.EndTime > startTime);

        if (userConflict)
        {
            TempData["Error"] = "You already have another active booking during this time slot.";
            return RedirectToAction(nameof(SelectSlot), new { stationId, chargerId });
        }

        var rate = station.ChargingRates.FirstOrDefault(r => r.ConnectorType == charger.ConnectorType && r.IsActive);
        decimal estimatedCost = 0;
        decimal appliedRate = rate?.RatePerKwh ?? 15.00m;
        if (rate != null)
        {
            estimatedCost = ((decimal)charger.CapacityKw * rate.RatePerKwh) + (rate.RatePerHour ?? 0);
        }

        var user = await _context.UserMasters.FindAsync(customerId);

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            CustomerName = user?.Fullname ?? "Customer",
            CustomerPhone = user?.Mobile,
            VehicleNumberPlate = vehicleNumberPlate?.Trim().ToUpper(),
            VehicleModel = vehicleModel?.Trim(),
            StationId = stationId,
            ChargerId = chargerId,
            BookingDate = dateOnly,
            StartTime = startTime,
            EndTime = endTime,
            Status = "Confirmed",
            AppliedRatePerKwh = appliedRate,
            EstimatedCost = estimatedCost,
            CreatedAt = DateTime.UtcNow
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        // Send booking confirmation email in background
        _ = SendBookingConfirmationEmailAsync(booking.Id);

        TempData["Success"] = $"Booking confirmed for {station.Name} ({charger.Name}) on {dateOnly:dd MMM yyyy} at {startTime:hh\\:mm}!";
        return Redirect("/customer/bookings");
    }

    // ── JOIN QUEUE / WAITLIST ──────────────────────────────────────────
    // POST: /customer/book/join-waitlist
    [HttpPost("join-waitlist")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> JoinWaitlist(Guid stationId, Guid chargerId, DateTime bookingDate, TimeSpan startTime, TimeSpan endTime, string? vehicleNumberPlate)
    {
        var customerId = GetUserId();
        var dateOnly = bookingDate.Date;

        // Check if already in waitlist
        var existing = await _context.WaitlistEntries
            .FirstOrDefaultAsync(w => w.ChargerId == chargerId && w.BookingDate == dateOnly && w.StartTime == startTime && w.CustomerId == customerId && w.Status == "Waiting");

        if (existing != null)
        {
            TempData["Error"] = "You are already in the waitlist queue for this slot.";
            return RedirectToAction(nameof(SelectSlot), new { stationId, chargerId, date = dateOnly.ToString("yyyy-MM-dd") });
        }

        var currentQueueLength = await _context.WaitlistEntries
            .CountAsync(w => w.ChargerId == chargerId && w.BookingDate == dateOnly && w.StartTime == startTime && w.Status == "Waiting");

        var waitlist = new WaitlistEntry
        {
            Id = Guid.NewGuid(),
            StationId = stationId,
            ChargerId = chargerId,
            CustomerId = customerId,
            BookingDate = dateOnly,
            StartTime = startTime,
            EndTime = endTime,
            QueuePosition = currentQueueLength + 1,
            Status = "Waiting",
            VehicleNumberPlate = vehicleNumberPlate?.Trim().ToUpper(),
            CreatedAt = DateTime.UtcNow
        };

        _context.WaitlistEntries.Add(waitlist);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"🔔 You have joined the queue for slot {startTime:hh\\:mm} (Position #{waitlist.QueuePosition})! If the current driver cancels, this slot will automatically be given to you.";
        return RedirectToAction(nameof(SelectSlot), new { stationId, chargerId, date = dateOnly.ToString("yyyy-MM-dd") });
    }

    private async Task SendBookingConfirmationEmailAsync(Guid bookingId)
    {
        try
        {
            var booking = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Station)
                .Include(b => b.Charger)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null || booking.Customer == null || string.IsNullOrEmpty(booking.Customer.Email))
                return;

            var client = _httpClientFactory.CreateClient();
            var apiKey = _configuration["MailApi:ApiKey"];
            if (!string.IsNullOrEmpty(apiKey))
            {
                client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            }

            var formContent = new MultipartFormDataContent();
            formContent.Add(new StringContent(booking.Customer.Email), "to");
            formContent.Add(new StringContent($"VoltNet Booking Confirmation — #{booking.Id.ToString().Substring(0, 8).ToUpper()}"), "subject");

            var body = $@"
Hello {booking.Customer.Fullname},

Your EV charging slot has been successfully reserved with VoltNet!

Reservation Summary:
• Booking ID: #{booking.Id.ToString().Substring(0, 8).ToUpper()}
• Station: {booking.Station?.Name}, {booking.Station?.Address}, {booking.Station?.City}
• Charger: {booking.Charger?.Name} ({booking.Charger?.ConnectorType}, {booking.Charger?.CapacityKw}kW)
• Vehicle Plate: {booking.VehicleNumberPlate ?? "Not specified"}
• Date: {booking.BookingDate:dd MMM yyyy}
• Time Slot: {booking.StartTime:hh\:mm} - {booking.EndTime:hh\:mm}
• Billing Mode: Units used (kWh) × Rate/kWh (₹{booking.AppliedRatePerKwh ?? 15.00m:F2}/kWh)
• Estimated Cost: ₹{booking.EstimatedCost:F2}
• Status: Confirmed

Note: When you finish charging at the station, your total bill will be accurately calculated based on actual energy meter units consumed.

Thank you for driving electric with VoltNet!
VoltNet Team";

            formContent.Add(new StringContent(body), "body");
            await client.PostAsync("http://mailsendapi.runasp.net/api/Mailing/send", formContent);
        }
        catch
        {
            // Background email exception won't fail transaction
        }
    }

    private static List<(TimeSpan StartTime, TimeSpan EndTime)> GenerateStationSlots(TimeSpan opening, TimeSpan closing, int slotDurationMinutes)
    {
        var slots = new List<(TimeSpan StartTime, TimeSpan EndTime)>();
        var current = opening;

        if (closing > opening)
        {
            while (current + TimeSpan.FromMinutes(slotDurationMinutes) <= closing)
            {
                slots.Add((current, current + TimeSpan.FromMinutes(slotDurationMinutes)));
                current += TimeSpan.FromMinutes(slotDurationMinutes);
            }
        }
        else
        {
            var midnight = TimeSpan.FromHours(24);
            while (current + TimeSpan.FromMinutes(slotDurationMinutes) <= midnight)
            {
                slots.Add((current, current + TimeSpan.FromMinutes(slotDurationMinutes)));
                current += TimeSpan.FromMinutes(slotDurationMinutes);
            }
        }

        return slots;
    }
}

public class SlotViewModel
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsBooked { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }
    public bool IsPast { get; set; }
    public int WaitlistCount { get; set; }
    public bool IsUserInWaitlist { get; set; }
}
