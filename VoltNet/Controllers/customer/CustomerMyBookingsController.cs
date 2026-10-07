using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VoltNet.Data;
using VoltNet.Models;

namespace VoltNet.Controllers.customer;

[Authorize(Roles = "Customer")]
[Route("customer/bookings")]
public class CustomerMyBookingsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public CustomerMyBookingsController(AppDbContext context, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue("UserId")!);

    // GET: /customer/bookings
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var customerId = GetUserId();

        var bookings = await _context.Bookings
            .Include(b => b.Station)
            .Include(b => b.Charger)
            .Where(b => b.CustomerId == customerId)
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.StartTime)
            .ToListAsync();

        var myWaitlist = await _context.WaitlistEntries
            .Include(w => w.Station)
            .Include(w => w.Charger)
            .Where(w => w.CustomerId == customerId && w.Status == "Waiting")
            .OrderBy(w => w.BookingDate)
            .ThenBy(w => w.StartTime)
            .ToListAsync();

        ViewBag.Waitlist = myWaitlist;

        return View("~/Views/customer/Booking/MyBookings.cshtml", bookings);
    }

    // POST: /customer/bookings/cancel
    [HttpPost("cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, string cancellationReason)
    {
        var customerId = GetUserId();

        var booking = await _context.Bookings
            .Include(b => b.Station)
            .Include(b => b.Charger)
            .FirstOrDefaultAsync(b => b.Id == id && b.CustomerId == customerId);

        if (booking == null)
            return NotFound();

        if (booking.Status != "Confirmed")
        {
            TempData["Error"] = "Only confirmed bookings can be cancelled.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(cancellationReason))
        {
            TempData["Error"] = "Please provide a reason for cancelling your booking.";
            return RedirectToAction(nameof(Index));
        }

        // 30-Minute Cancellation Window Check (India Standard Time)
        var ist = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        var nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ist);
        var slotStartDateTime = booking.BookingDate.Date + booking.StartTime;

        if (slotStartDateTime <= nowIst)
        {
            TempData["Error"] = "Cannot cancel a booking after its start time has passed.";
            return RedirectToAction(nameof(Index));
        }

        var minutesRemaining = (slotStartDateTime - nowIst).TotalMinutes;
        if (minutesRemaining < 30)
        {
            TempData["Error"] = "Cancellations are only allowed at least 30 minutes before the scheduled slot start time.";
            return RedirectToAction(nameof(Index));
        }

        booking.Status = "Cancelled";
        booking.CancellationReason = cancellationReason.Trim();
        booking.CancelledAt = DateTime.UtcNow;

        // ── AUTOMATIC QUEUE PROMOTION ──
        // Check if anyone is waiting in the queue for this exact slot
        var nextInQueue = await _context.WaitlistEntries
            .Include(w => w.Customer)
            .Where(w => w.ChargerId == booking.ChargerId && w.BookingDate == booking.BookingDate && w.StartTime == booking.StartTime && w.Status == "Waiting")
            .OrderBy(w => w.QueuePosition)
            .ThenBy(w => w.CreatedAt)
            .FirstOrDefaultAsync();

        if (nextInQueue != null)
        {
            // Promote next in line to Confirmed Booking
            var promotedBooking = new Booking
            {
                Id = Guid.NewGuid(),
                CustomerId = nextInQueue.CustomerId,
                CustomerName = nextInQueue.Customer?.Fullname ?? "Customer",
                CustomerPhone = nextInQueue.Customer?.Mobile,
                VehicleNumberPlate = nextInQueue.VehicleNumberPlate,
                StationId = booking.StationId,
                ChargerId = booking.ChargerId,
                BookingDate = booking.BookingDate,
                StartTime = booking.StartTime,
                EndTime = booking.EndTime,
                Status = "Confirmed",
                AppliedRatePerKwh = booking.AppliedRatePerKwh,
                EstimatedCost = booking.EstimatedCost,
                CreatedAt = DateTime.UtcNow
            };

            nextInQueue.Status = "Promoted";
            nextInQueue.PromotedAt = DateTime.UtcNow;

            _context.Bookings.Add(promotedBooking);

            // Re-order remaining waitlist
            var remainingQueue = await _context.WaitlistEntries
                .Where(w => w.ChargerId == booking.ChargerId && w.BookingDate == booking.BookingDate && w.StartTime == booking.StartTime && w.Status == "Waiting" && w.Id != nextInQueue.Id)
                .OrderBy(w => w.QueuePosition)
                .ToListAsync();

            int pos = 1;
            foreach (var item in remainingQueue)
            {
                item.QueuePosition = pos++;
            }

            // Send promotion notification in background
            _ = SendPromotionNotificationEmailAsync(promotedBooking.Id);
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = "Booking has been cancelled and your slot has been released.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /customer/bookings/cancel-waitlist
    [HttpPost("cancel-waitlist")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelWaitlist(Guid id)
    {
        var customerId = GetUserId();

        var entry = await _context.WaitlistEntries
            .FirstOrDefaultAsync(w => w.Id == id && w.CustomerId == customerId);

        if (entry != null)
        {
            entry.Status = "Cancelled";
            await _context.SaveChangesAsync();
            TempData["Success"] = "You have left the waitlist queue.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task SendPromotionNotificationEmailAsync(Guid bookingId)
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
            formContent.Add(new StringContent($"🎉 Good News! You've Been Promoted to a Confirmed Slot — VoltNet"), "subject");

            var body = $@"
Hello {booking.Customer.Fullname},

A previously booked charging slot at {booking.Station?.Name} was just cancelled by another driver, and you have been AUTOMATICALLY PROMOTED from the waitlist queue to a CONFIRMED RESERVATION!

Your Booking Details:
• Station: {booking.Station?.Name}, {booking.Station?.City}
• Charger: {booking.Charger?.Name} ({booking.Charger?.ConnectorType})
• Date: {booking.BookingDate:dd MMM yyyy}
• Time Slot: {booking.StartTime:hh\:mm} - {booking.EndTime:hh\:mm}
• Status: Confirmed

View your booking here: http://localhost:5000/customer/bookings

Thank you for charging with VoltNet!
VoltNet Team";

            formContent.Add(new StringContent(body), "body");
            await client.PostAsync("http://mailsendapi.runasp.net/api/Mailing/send", formContent);
        }
        catch
        {
            // Email failure won't fail transaction
        }
    }
}
