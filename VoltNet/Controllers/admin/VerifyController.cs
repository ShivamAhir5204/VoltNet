using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VoltNet.Data;
using VoltNet.Models;
using System.Security.Claims;
using System.Net.Http;

namespace VoltNet.Controllers.admin;

[Authorize(Roles = "Admin,SuperAdmin")]
public class VerifyController : Controller
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public VerifyController(AppDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
    }

    private Guid? GetAdminId()
    {
        var idStr = User.FindFirstValue("AdminId");
        if (Guid.TryParse(idStr, out var id)) return id;
        return null;
    }

    // ── Verify Stations ─────────────────────────────────────────

    [HttpGet("admin/VerifyStations")]
    public async Task<IActionResult> VerifyStations()
    {
        var pendingStations = await _context.Stations
            .Include(s => s.OwnerUser)
            .Include(s => s.Documents)
            .Where(s => s.Status == "PendingVerification" || s.Documents.Any(d => d.Status == "Pending"))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return View("~/Views/admin/verify/VerifyStations.cshtml", pendingStations);
    }

    [HttpGet("admin/VerifyStations/Details/{id}")]
    [HttpGet("admin/VerifyStations/StationDetails/{id}")]
    [HttpGet("admin/Verify/StationDetails/{id}")]
    [HttpPost("admin/VerifyStations/Details")]
    public async Task<IActionResult> StationDetails(Guid id)
    {
        var station = await _context.Stations
            .Include(s => s.OwnerUser)
            .Include(s => s.Documents)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (station == null)
        {
            TempData["Error"] = "The requested charging station could not be found.";
            return RedirectToAction(nameof(VerifyStations));
        }

        return View("~/Views/admin/verify/StationDetails.cshtml", station);
    }

    [HttpPost("admin/VerifyStations/Approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveStation(Guid id)
    {
        var station = await _context.Stations
            .Include(s => s.OwnerUser)
            .Include(s => s.Documents)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (station == null) return NotFound();

        var adminId = GetAdminId();

        station.Status = "Active";
        station.RejectionReason = null;

        // Auto-verify all remaining pending documents
        foreach (var doc in station.Documents.Where(d => d.Status == "Pending"))
        {
            doc.Status = "Verified";
            doc.VerifiedAt = DateTime.UtcNow;
            doc.VerifiedByAdminId = adminId;
        }

        // Auto-assign to owner's active subscription if available
        if (station.OwnerSubscriptionId == null && station.OwnerUserId != Guid.Empty)
        {
            var activeSub = await _context.OwnerSubscriptions
                .Include(os => os.Plan)
                .Include(os => os.Stations)
                .FirstOrDefaultAsync(os => os.Owner!.UserId == station.OwnerUserId && os.Status == "Active" && os.EndDate >= DateTime.UtcNow);

            if (activeSub != null && (activeSub.Plan == null || activeSub.Stations.Count < activeSub.Plan.MaxStations))
            {
                station.OwnerSubscriptionId = activeSub.Id;
            }
        }

        await _context.SaveChangesAsync();

        // Send Email Notification to Owner
        if (station.OwnerUser != null && !string.IsNullOrWhiteSpace(station.OwnerUser.Email))
        {
            try
            {
                var openingStr = station.OpeningTime.ToString(@"hh\:mm");
                var closingStr = station.ClosingTime.ToString(@"hh\:mm");
                var subject = $"VoltNet — Station Approved: {station.Name}";
                var body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; padding: 24px; background: #ffffff;'>
                        <div style='text-align: center; margin-bottom: 20px;'>
                            <h2 style='color: #2e9d54; margin: 0;'>⚡ VoltNet EV Network</h2>
                            <p style='color: #64748b; font-size: 14px;'>Station Verification Department</p>
                        </div>
                        <div style='background: #f0fdf4; border-left: 4px solid #22c55e; padding: 16px; border-radius: 4px; margin-bottom: 20px;'>
                            <h3 style='color: #15803d; margin: 0 0 8px 0;'>Station Approved & Live!</h3>
                            <p style='color: #166534; margin: 0; font-size: 14px;'>
                                Congratulations! Your charging station <strong>{station.Name}</strong> has been verified and approved by the VoltNet Administration team.
                            </p>
                        </div>
                        <table style='width: 100%; border-collapse: collapse; margin-bottom: 20px; font-size: 14px;'>
                            <tr><td style='padding: 8px 0; color: #64748b;'>Station Name:</td><td style='padding: 8px 0; font-weight: bold; color: #0f172a;'>{station.Name}</td></tr>
                            <tr><td style='padding: 8px 0; color: #64748b;'>Address:</td><td style='padding: 8px 0; color: #0f172a;'>{station.Address}, {station.City}, {station.State}</td></tr>
                            <tr><td style='padding: 8px 0; color: #64748b;'>Operating Hours:</td><td style='padding: 8px 0; color: #0f172a;'>{openingStr} - {closingStr}</td></tr>
                            <tr><td style='padding: 8px 0; color: #64748b;'>Status:</td><td style='padding: 8px 0;'><span style='background: #dcfce7; color: #15803d; padding: 4px 10px; border-radius: 9999px; font-weight: bold;'>Active</span></td></tr>
                        </table>
                        <p style='color: #475569; font-size: 14px; line-height: 1.6;'>
                            Your station is now publicly discoverable by EV drivers on the VoltNet map. You can manage chargers, hourly tariffs, and assign station managers directly from your Station Owner Dashboard.
                        </p>
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='http://localhost:5213/owner/dashboard' style='background: #2e9d54; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>Access Owner Dashboard</a>
                        </div>
                        <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 30px 0 15px 0;' />
                        <p style='color: #94a3b8; font-size: 12px; text-align: center;'>This is an automated notification from VoltNet Charging Infrastructure Platform.</p>
                    </div>";
                await SendEmailNotificationAsync(station.OwnerUser.Email, subject, body);
            }
            catch
            {
                // Ensure notification failure never blocks the approval process
            }
        }

        TempData["Success"] = $"Station '{station.Name}' approved successfully. Owner has been notified via email.";
        return RedirectToAction(nameof(VerifyStations));
    }

    [HttpPost("admin/VerifyStations/Reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectStation(Guid id, string rejectionReason)
    {
        var station = await _context.Stations
            .Include(s => s.OwnerUser)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (station == null) return NotFound();

        station.Status = "Rejected";
        station.RejectionReason = rejectionReason;
        await _context.SaveChangesAsync();

        // Send Email Notification to Owner
        if (station.OwnerUser != null && !string.IsNullOrWhiteSpace(station.OwnerUser.Email))
        {
            try
            {
                var subject = $"VoltNet — Action Required: Station '{station.Name}' Verification";
                var body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; padding: 24px; background: #ffffff;'>
                        <div style='text-align: center; margin-bottom: 20px;'>
                            <h2 style='color: #2e9d54; margin: 0;'>⚡ VoltNet EV Network</h2>
                            <p style='color: #64748b; font-size: 14px;'>Station Verification Department</p>
                        </div>
                        <div style='background: #fef2f2; border-left: 4px solid #ef4444; padding: 16px; border-radius: 4px; margin-bottom: 20px;'>
                            <h3 style='color: #b91c1c; margin: 0 0 8px 0;'>Verification Requires Updates</h3>
                            <p style='color: #991b1b; margin: 0; font-size: 14px;'>
                                Your charging station <strong>{station.Name}</strong> could not be approved at this time.
                            </p>
                        </div>
                        <div style='background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 16px; margin-bottom: 20px;'>
                            <strong style='color: #0f172a; font-size: 14px; display: block; margin-bottom: 6px;'>Reason for Rejection / Feedback:</strong>
                            <p style='color: #475569; margin: 0; font-size: 14px; line-height: 1.6;'>{rejectionReason}</p>
                        </div>
                        <p style='color: #475569; font-size: 14px; line-height: 1.6;'>
                            Please sign in to your Station Owner portal, review your station compliance documents, and re-upload the corrected files. Once updated, your station will automatically re-enter the verification queue.
                        </p>
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='http://localhost:5213/owner/stations' style='background: #0f172a; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>View Station Details & Re-upload</a>
                        </div>
                        <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 30px 0 15px 0;' />
                        <p style='color: #94a3b8; font-size: 12px; text-align: center;'>This is an automated notification from VoltNet Charging Infrastructure Platform.</p>
                    </div>";
                await SendEmailNotificationAsync(station.OwnerUser.Email, subject, body);
            }
            catch
            {
                // Ensure notification failure never blocks the rejection process
            }
        }

        TempData["Success"] = $"Station '{station.Name}' has been rejected. Feedback email dispatched to owner.";
        return RedirectToAction(nameof(VerifyStations));
    }

    // ── Verify Owners (Become Partner) ──────────────────────────

    [HttpGet("admin/VerifyOwners")]
    public async Task<IActionResult> VerifyOwners()
    {
        var pendingOwners = await _context.StationOwners
            .Include(o => o.User)
            .Include(o => o.Documents)
            .Where(o => o.Status == "Pending")
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return View("~/Views/admin/verify/VerifyOwners.cshtml", pendingOwners);
    }

    [HttpGet("admin/VerifyOwners/Details/{id}")]
    [HttpGet("admin/VerifyOwners/OwnerDetails/{id}")]
    [HttpGet("admin/Verify/OwnerDetails/{id}")]
    [HttpPost("admin/VerifyOwners/Details")]
    public async Task<IActionResult> OwnerDetails(Guid id)
    {
        var owner = await _context.StationOwners
            .Include(o => o.User)
            .Include(o => o.Documents)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (owner == null)
        {
            TempData["Error"] = "The requested station owner could not be found.";
            return RedirectToAction(nameof(VerifyOwners));
        }

        return View("~/Views/admin/verify/OwnerDetails.cshtml", owner);
    }

    [HttpPost("admin/VerifyOwners/Approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveOwner(Guid id)
    {
        var owner = await _context.StationOwners
            .Include(o => o.User)
            .Include(o => o.Documents)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (owner == null) return NotFound();

        var adminId = GetAdminId();

        owner.Status = "Approved";
        owner.ApprovedAt = DateTime.UtcNow;
        owner.RejectionReason = null;

        // Change user role to StationOwner
        owner.User.Role = "StationOwner";

        // Auto-verify all pending documents
        foreach (var doc in owner.Documents.Where(d => d.Status == "Pending"))
        {
            doc.Status = "Verified";
            doc.VerifiedAt = DateTime.UtcNow;
            doc.VerifiedByAdminId = adminId;
        }

        await _context.SaveChangesAsync();

        // Send Email Notification to Applicant
        if (owner.User != null && !string.IsNullOrWhiteSpace(owner.User.Email))
        {
            try
            {
                var subject = "VoltNet — Partner Application Approved!";
                var body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; padding: 24px; background: #ffffff;'>
                        <div style='text-align: center; margin-bottom: 20px;'>
                            <h2 style='color: #2e9d54; margin: 0;'>⚡ VoltNet EV Network</h2>
                            <p style='color: #64748b; font-size: 14px;'>Partner Onboarding Division</p>
                        </div>
                        <div style='background: #f0fdf4; border-left: 4px solid #22c55e; padding: 16px; border-radius: 4px; margin-bottom: 20px;'>
                            <h3 style='color: #15803d; margin: 0 0 8px 0;'>Welcome to VoltNet Partner Ecosystem!</h3>
                            <p style='color: #166534; margin: 0; font-size: 14px;'>
                                Dear <strong>{owner.FullName}</strong>, your application to become a Station Owner has been thoroughly verified and approved!
                            </p>
                        </div>
                        <p style='color: #475569; font-size: 14px; line-height: 1.6;'>
                            Your VoltNet account has been elevated to <strong>Station Owner</strong>. You now have complete access to:
                        </p>
                        <ul style='color: #475569; font-size: 14px; line-height: 1.8;'>
                            <li>Create and operate commercial EV charging stations.</li>
                            <li>Assign and manage on-site station staff and managers.</li>
                            <li>Configure charging bay tariffs and track petrol-pump meter sessions.</li>
                            <li>Subscribe to VoltNet Station Owner growth plans.</li>
                        </ul>
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='http://localhost:5213/login' style='background: #2e9d54; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>Sign In as Station Owner</a>
                        </div>
                        <p style='color: #64748b; font-size: 13px;'>Note: If you are currently logged in, please log out and log back in to refresh your active session role.</p>
                        <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 30px 0 15px 0;' />
                        <p style='color: #94a3b8; font-size: 12px; text-align: center;'>This is an automated notification from VoltNet Charging Infrastructure Platform.</p>
                    </div>";
                await SendEmailNotificationAsync(owner.User.Email, subject, body);
            }
            catch
            {
                // Ensure notification failure never blocks the approval process
            }
        }

        TempData["Success"] = $"Partner application for '{owner.FullName}' approved. Role updated to Station Owner and welcome email dispatched.";
        return RedirectToAction(nameof(VerifyOwners));
    }

    [HttpPost("admin/VerifyOwners/Reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectOwner(Guid id, string rejectionReason)
    {
        var owner = await _context.StationOwners
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (owner == null) return NotFound();

        owner.Status = "Rejected";
        owner.RejectionReason = rejectionReason;

        await _context.SaveChangesAsync();

        // Send Email Notification to Applicant
        if (owner.User != null && !string.IsNullOrWhiteSpace(owner.User.Email))
        {
            try
            {
                var subject = "VoltNet — Partner Application Status Update";
                var body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; padding: 24px; background: #ffffff;'>
                        <div style='text-align: center; margin-bottom: 20px;'>
                            <h2 style='color: #2e9d54; margin: 0;'>⚡ VoltNet EV Network</h2>
                            <p style='color: #64748b; font-size: 14px;'>Partner Onboarding Division</p>
                        </div>
                        <div style='background: #fef2f2; border-left: 4px solid #ef4444; padding: 16px; border-radius: 4px; margin-bottom: 20px;'>
                            <h3 style='color: #b91c1c; margin: 0 0 8px 0;'>Application Requires Changes</h3>
                            <p style='color: #991b1b; margin: 0; font-size: 14px;'>
                                Dear {owner.FullName}, your partner application requires additional or corrected documentation.
                            </p>
                        </div>
                        <div style='background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 16px; margin-bottom: 20px;'>
                            <strong style='color: #0f172a; font-size: 14px; display: block; margin-bottom: 6px;'>Feedback / Remarks:</strong>
                            <p style='color: #475569; margin: 0; font-size: 14px; line-height: 1.6;'>{rejectionReason}</p>
                        </div>
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='http://localhost:5213/partner/status' style='background: #0f172a; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>View Application & Re-upload</a>
                        </div>
                        <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 30px 0 15px 0;' />
                        <p style='color: #94a3b8; font-size: 12px; text-align: center;'>This is an automated notification from VoltNet Charging Infrastructure Platform.</p>
                    </div>";
                await SendEmailNotificationAsync(owner.User.Email, subject, body);
            }
            catch
            {
                // Ensure notification failure never blocks the rejection process
            }
        }

        TempData["Success"] = $"Partner application for '{owner.FullName}' has been rejected. Notification email sent.";
        return RedirectToAction(nameof(VerifyOwners));
    }

    // ── Individual Document Decision Actions ────────────────────

    [HttpPost("admin/VerifyDocuments/VerifyStationDocument")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyStationDocument(Guid documentId, Guid stationId)
    {
        var doc = await _context.StationDocuments
            .Include(d => d.Station)
            .ThenInclude(s => s.OwnerUser)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.StationId == stationId);

        if (doc != null)
        {
            doc.Status = "Verified";
            doc.VerifiedAt = DateTime.UtcNow;
            doc.VerifiedByAdminId = GetAdminId();
            doc.RejectionReason = null;
            await _context.SaveChangesAsync();

            // Send notification email to owner
            if (doc.Station?.OwnerUser != null && !string.IsNullOrWhiteSpace(doc.Station.OwnerUser.Email))
            {
                try
                {
                    var subject = $"VoltNet — Document Verified: {doc.DocumentType}";
                    var body = $@"
                        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; padding: 24px; background: #ffffff;'>
                            <h2 style='color: #2e9d54; margin-top: 0;'>⚡ VoltNet Verification Update</h2>
                            <p style='color: #334155;'>Your compliance document <strong>{doc.DocumentType}</strong> for station <strong>{doc.Station.Name}</strong> has been successfully verified and approved by the VoltNet Administration team.</p>
                            <p style='color: #64748b; font-size: 13px;'>No further action is required for this document.</p>
                        </div>";
                    await SendEmailNotificationAsync(doc.Station.OwnerUser.Email, subject, body);
                }
                catch
                {
                    // Ensure notification failure never blocks the document verification
                }
            }

            TempData["Success"] = $"Document '{doc.DocumentType}' verified successfully.";
        }
        return RedirectToAction(nameof(StationDetails), new { id = stationId });
    }

    [HttpPost("admin/VerifyDocuments/RejectStationDocument")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectStationDocument(Guid documentId, Guid stationId, string rejectionReason)
    {
        var doc = await _context.StationDocuments
            .Include(d => d.Station)
            .ThenInclude(s => s.OwnerUser)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.StationId == stationId);

        if (doc != null)
        {
            doc.Status = "Rejected";
            doc.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? "Document invalid or unreadable." : rejectionReason.Trim();
            await _context.SaveChangesAsync();

            // Send notification email to owner
            if (doc.Station?.OwnerUser != null && !string.IsNullOrWhiteSpace(doc.Station.OwnerUser.Email))
            {
                try
                {
                    var subject = $"VoltNet — Action Required: Document '{doc.DocumentType}' Rejected";
                    var body = $@"
                        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; padding: 24px; background: #ffffff;'>
                            <h2 style='color: #dc2626; margin-top: 0;'>⚡ VoltNet Verification Update</h2>
                            <p style='color: #334155;'>Your compliance document <strong>{doc.DocumentType}</strong> for station <strong>{doc.Station.Name}</strong> was rejected during verification.</p>
                            <div style='background: #fef2f2; border-left: 4px solid #ef4444; padding: 12px; margin: 15px 0;'>
                                <strong style='color: #991b1b;'>Reason for Rejection:</strong>
                                <p style='margin: 4px 0 0 0; color: #991b1b;'>{doc.RejectionReason}</p>
                            </div>
                            <p style='color: #334155;'>Please log in to your Station Owner portal, open your station details, and re-upload the corrected document.</p>
                        </div>";
                    await SendEmailNotificationAsync(doc.Station.OwnerUser.Email, subject, body);
                }
                catch
                {
                    // Ensure notification failure never blocks document rejection
                }
            }

            TempData["Success"] = $"Document '{doc.DocumentType}' marked as rejected and notification email sent.";
        }
        return RedirectToAction(nameof(StationDetails), new { id = stationId });
    }

    [HttpPost("admin/VerifyDocuments/VerifyOwnerDocument")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOwnerDocument(Guid documentId, Guid ownerId)
    {
        var doc = await _context.StationOwnerDocuments.FindAsync(documentId);
        if (doc != null && doc.StationOwnerId == ownerId)
        {
            doc.Status = "Verified";
            doc.VerifiedAt = DateTime.UtcNow;
            doc.VerifiedByAdminId = GetAdminId();
            doc.RejectionReason = null;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"KYC Document '{doc.DocumentType}' verified.";
        }
        return RedirectToAction(nameof(OwnerDetails), new { id = ownerId });
    }

    [HttpPost("admin/VerifyDocuments/RejectOwnerDocument")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectOwnerDocument(Guid documentId, Guid ownerId, string rejectionReason)
    {
        var doc = await _context.StationOwnerDocuments.FindAsync(documentId);
        if (doc != null && doc.StationOwnerId == ownerId)
        {
            doc.Status = "Rejected";
            doc.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? "Document invalid or unreadable." : rejectionReason.Trim();
            await _context.SaveChangesAsync();
            TempData["Success"] = $"KYC Document '{doc.DocumentType}' marked as rejected.";
        }
        return RedirectToAction(nameof(OwnerDetails), new { id = ownerId });
    }

    private async Task SendEmailNotificationAsync(string toEmail, string subject, string body)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var formContent = new MultipartFormDataContent();
            formContent.Add(new StringContent(toEmail), "to");
            formContent.Add(new StringContent(subject), "subject");
            formContent.Add(new StringContent(body), "body");

            await client.PostAsync("http://mailsendapi.runasp.net/api/Mailing/send", formContent);
        }
        catch
        {
            // Silently log or continue to ensure core DB transactions never fail
        }
    }
}
