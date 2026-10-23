using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VoltNet.Models;

namespace VoltNet.Services;

/// <summary>
/// Native PDF generator that produces compliant PDF 1.4 documents without third-party runtime dependencies.
/// Generates commercial receipts and revenue analytical reports.
/// </summary>
public static class PdfGeneratorService
{
    public static byte[] GenerateSubscriptionReceiptPdf(OwnerSubscription subscription, UserMaster? user)
    {
        var doc = new SimplePdfWriter(595, 842); // A4 standard (points)

        // ── Brand Header Banner ──
        doc.FillRect(0, 742, 595, 100, 0.08f, 0.13f, 0.24f); // Dark Slate Navy #152238
        doc.DrawText(40, 802, "VOLTNET CHARGING INFRASTRUCTURE", 16, true, 1f, 1f, 1f);
        doc.DrawText(40, 782, "TAX INVOICE & SUBSCRIPTION PAYMENT RECEIPT", 10, false, 0.38f, 0.75f, 0.98f);
        doc.DrawText(40, 762, "GSTIN: 24AAACV1234F1Z8 | CIN: U40106GJ2026PTC109876", 8, false, 0.7f, 0.75f, 0.85f);

        // Header Right: Receipt Meta
        var receiptNo = !string.IsNullOrWhiteSpace(subscription.PaymentId) 
            ? $"RCP-{subscription.PaymentId.Trim()}" 
            : $"RCP-{subscription.Id.ToString()[..8].ToUpper()}";
        doc.DrawText(380, 802, $"Receipt No: {receiptNo}", 9, true, 1f, 1f, 1f);
        doc.DrawText(380, 786, $"Date: {subscription.CreatedAt:dd MMM yyyy, hh:mm tt} UTC", 8, false, 0.8f, 0.85f, 0.9f);
        doc.DrawText(380, 770, $"Status: {subscription.Status.ToUpper()}", 9, true, 0.2f, 0.8f, 0.4f);

        // ── Two Info Columns (Bill To & Platform Details) ──
        float y = 710;
        doc.FillRect(40, y - 75, 245, 85, 0.96f, 0.97f, 0.99f);
        doc.DrawRect(40, y - 75, 245, 85, 0.85f, 0.88f, 0.92f);
        doc.DrawText(50, y - 5, "BILLED TO / SUBSCRIBER", 9, true, 0.15f, 0.2f, 0.35f);
        doc.DrawText(50, y - 22, $"Name: {Sanitize(user?.Fullname ?? "Station Partner")}", 9, false, 0.1f, 0.1f, 0.15f);
        doc.DrawText(50, y - 37, $"Email: {Sanitize(user?.Email ?? "N/A")}", 8, false, 0.3f, 0.35f, 0.4f);
        doc.DrawText(50, y - 52, $"Mobile: {Sanitize(user?.Mobile ?? "N/A")}", 8, false, 0.3f, 0.35f, 0.4f);
        doc.DrawText(50, y - 67, $"City/State: {Sanitize(user?.City ?? "Gujarat")}, {Sanitize(user?.State ?? "India")}", 8, false, 0.3f, 0.35f, 0.4f);

        doc.FillRect(310, y - 75, 245, 85, 0.96f, 0.97f, 0.99f);
        doc.DrawRect(310, y - 75, 245, 85, 0.85f, 0.88f, 0.92f);
        doc.DrawText(320, y - 5, "SUBSCRIPTION METADATA", 9, true, 0.15f, 0.2f, 0.35f);
        doc.DrawText(320, y - 22, $"Plan Tier: {Sanitize(subscription.Plan?.Name ?? "Commercial Plan")}", 9, true, 0.1f, 0.5f, 0.3f);
        doc.DrawText(320, y - 37, $"Duration: {subscription.Plan?.DurationDays ?? 30} Days", 8, false, 0.3f, 0.35f, 0.4f);
        doc.DrawText(320, y - 52, $"Start Date: {subscription.StartDate:dd MMM yyyy}", 8, false, 0.3f, 0.35f, 0.4f);
        doc.DrawText(320, y - 67, $"Valid Till: {subscription.EndDate:dd MMM yyyy}", 8, false, 0.3f, 0.35f, 0.4f);

        // ── Plan Quotas & Entitlements Table ──
        y = 605;
        doc.DrawText(40, y + 10, "PLAN ENTITLEMENTS & LIMITS", 10, true, 0.1f, 0.15f, 0.3f);
        doc.FillRect(40, y - 18, 515, 22, 0.15f, 0.22f, 0.38f);
        doc.DrawText(50, y - 4, "Feature / Allocation", 9, true, 1f, 1f, 1f);
        doc.DrawText(350, y - 4, "Authorized Quota", 9, true, 1f, 1f, 1f);
        doc.DrawText(470, y - 4, "Status", 9, true, 1f, 1f, 1f);

        float tableY = y - 40;
        var features = new[]
        {
            ("Maximum Active Charging Stations", $"{subscription.Plan?.MaxStations ?? 1} Station(s)", "Allocated"),
            ("Maximum Managers Per Station", $"{subscription.Plan?.MaxManagersPerStation ?? 2} Manager(s)", "Allocated"),
            ("IoT Real-time Charger Control & Telemetry", "Enabled", "Active"),
            ("Automated Spot Booking & Waitlist Queue", "Included", "Active"),
            ("Financial & Dynamic Tariff Analytics", "Full Access", "Active")
        };

        foreach (var (feat, quota, status) in features)
        {
            doc.FillRect(40, tableY - 6, 515, 20, 0.98f, 0.98f, 0.99f);
            doc.DrawRect(40, tableY - 6, 515, 20, 0.9f, 0.92f, 0.95f);
            doc.DrawText(50, tableY + 5, feat, 8, false, 0.2f, 0.25f, 0.3f);
            doc.DrawText(350, tableY + 5, quota, 8, true, 0.15f, 0.2f, 0.3f);
            doc.DrawText(470, tableY + 5, status, 8, false, 0.1f, 0.6f, 0.2f);
            tableY -= 22;
        }

        // ── Financial Calculation & Payment Summary ──
        y = tableY - 20;
        doc.DrawText(40, y + 10, "PAYMENT BREAKDOWN", 10, true, 0.1f, 0.15f, 0.3f);
        doc.FillRect(40, y - 18, 515, 22, 0.15f, 0.22f, 0.38f);
        doc.DrawText(50, y - 4, "Description", 9, true, 1f, 1f, 1f);
        doc.DrawText(380, y - 4, "Taxable / Rate", 9, true, 1f, 1f, 1f);
        doc.DrawText(480, y - 4, "Amount (INR)", 9, true, 1f, 1f, 1f);

        var planPrice = subscription.AmountPaid;
        var baseAmount = Math.Round(planPrice / 1.18m, 2);
        var gstAmount = planPrice - baseAmount;
        var cgst = Math.Round(gstAmount / 2m, 2);
        var sgst = gstAmount - cgst;

        y -= 40;
        doc.DrawText(50, y + 5, $"VoltNet Platform Subscription ({subscription.Plan?.Name ?? "Plan"})", 8, false, 0.2f, 0.25f, 0.3f);
        doc.DrawText(380, y + 5, "Base Fee", 8, false, 0.4f, 0.45f, 0.5f);
        doc.DrawText(480, y + 5, $"Rs. {baseAmount:N2}", 8, false, 0.1f, 0.15f, 0.2f);
        y -= 18;

        doc.DrawText(50, y + 5, "CGST (9%)", 8, false, 0.4f, 0.45f, 0.5f);
        doc.DrawText(380, y + 5, "9.00%", 8, false, 0.4f, 0.45f, 0.5f);
        doc.DrawText(480, y + 5, $"Rs. {cgst:N2}", 8, false, 0.4f, 0.45f, 0.5f);
        y -= 18;

        doc.DrawText(50, y + 5, "SGST (9%)", 8, false, 0.4f, 0.45f, 0.5f);
        doc.DrawText(380, y + 5, "9.00%", 8, false, 0.4f, 0.45f, 0.5f);
        doc.DrawText(480, y + 5, $"Rs. {sgst:N2}", 8, false, 0.4f, 0.45f, 0.5f);
        y -= 25;

        // Total Highlight Box
        doc.FillRect(320, y - 8, 235, 30, 0.92f, 0.98f, 0.94f);
        doc.DrawRect(320, y - 8, 235, 30, 0.2f, 0.7f, 0.35f);
        doc.DrawText(330, y + 8, "TOTAL AMOUNT PAID:", 10, true, 0.05f, 0.45f, 0.15f);
        doc.DrawText(460, y + 8, $"Rs. {planPrice:N2}", 11, true, 0.05f, 0.5f, 0.15f);

        // ── Transaction Proof Box ──
        y -= 45;
        doc.FillRect(40, y - 28, 515, 35, 0.97f, 0.97f, 0.98f);
        doc.DrawRect(40, y - 28, 515, 35, 0.88f, 0.9f, 0.92f);
        doc.DrawText(50, y - 5, "Payment Method: Online Gateway (UPI / NetBanking / Cards)", 8, true, 0.2f, 0.25f, 0.3f);
        doc.DrawText(50, y - 20, $"Transaction Reference ID: {subscription.PaymentId ?? subscription.Id.ToString()}", 8, false, 0.35f, 0.4f, 0.45f);

        // ── Footer & Legal Disclaimer ──
        doc.DrawLine(40, 75, 555, 75, 0.85f, 0.88f, 0.92f);
        doc.DrawText(40, 60, "This is a computer generated commercial tax invoice and does not require a physical signature.", 8, false, 0.45f, 0.5f, 0.55f);
        doc.DrawText(40, 48, "VoltNet EV Infrastructure Systems India Pvt Ltd | support@voltnet.com | +91 1800-VOLT-NET", 8, false, 0.45f, 0.5f, 0.55f);
        doc.DrawText(450, 48, $"Generated: {DateTime.UtcNow:dd-MM-yyyy}", 8, false, 0.5f, 0.55f, 0.6f);

        return doc.Build();
    }

    public static byte[] GenerateRevenueReportPdf(
        string title,
        string entitySubtitle,
        DateTime startDate,
        DateTime endDate,
        decimal totalRevenue,
        int totalBookings,
        decimal totalUnitsKwh,
        List<Booking> bookings,
        string generatedBy)
    {
        var doc = new SimplePdfWriter(595, 842); // A4 standard

        // ── Brand Header Banner ──
        doc.FillRect(0, 742, 595, 100, 0.08f, 0.13f, 0.24f); // Slate Navy
        doc.DrawText(40, 802, "VOLTNET CHARGING INFRASTRUCTURE", 16, true, 1f, 1f, 1f);
        doc.DrawText(40, 782, Sanitize(title.ToUpper()), 11, false, 0.38f, 0.75f, 0.98f);
        doc.DrawText(40, 762, Sanitize(entitySubtitle), 9, false, 0.7f, 0.75f, 0.85f);

        // Header Right: Period Meta
        doc.DrawText(380, 802, "FINANCIAL STATEMENT", 9, true, 1f, 1f, 1f);
        doc.DrawText(380, 786, $"Period: {startDate:dd MMM yyyy} - {endDate:dd MMM yyyy}", 8, false, 0.85f, 0.9f, 0.95f);
        doc.DrawText(380, 770, $"Generated By: {Sanitize(generatedBy)}", 8, false, 0.75f, 0.8f, 0.85f);

        // ── KPI Summary Cards ──
        float y = 705;
        float cardWidth = 120;
        float gap = 11;

        // Card 1: Total Revenue
        doc.FillRect(40, y - 55, cardWidth, 60, 0.94f, 0.98f, 0.95f);
        doc.DrawRect(40, y - 55, cardWidth, 60, 0.2f, 0.7f, 0.35f);
        doc.DrawText(48, y - 12, "GROSS REVENUE", 7, true, 0.1f, 0.5f, 0.2f);
        doc.DrawText(48, y - 32, $"Rs. {totalRevenue:N2}", 11, true, 0.05f, 0.4f, 0.15f);
        doc.DrawText(48, y - 48, "Completed sessions", 6, false, 0.3f, 0.4f, 0.35f);

        // Card 2: Total Sessions
        float x2 = 40 + cardWidth + gap;
        doc.FillRect(x2, y - 55, cardWidth, 60, 0.95f, 0.97f, 1f);
        doc.DrawRect(x2, y - 55, cardWidth, 60, 0.3f, 0.5f, 0.9f);
        doc.DrawText(x2 + 8, y - 12, "TOTAL CHARGES", 7, true, 0.1f, 0.3f, 0.7f);
        doc.DrawText(x2 + 8, y - 32, $"{totalBookings} Sessions", 11, true, 0.1f, 0.25f, 0.6f);
        doc.DrawText(x2 + 8, y - 48, "100% fulfilled", 6, false, 0.35f, 0.4f, 0.5f);

        // Card 3: Energy Dispensed
        float x3 = x2 + cardWidth + gap;
        doc.FillRect(x3, y - 55, cardWidth, 60, 0.99f, 0.97f, 0.93f);
        doc.DrawRect(x3, y - 55, cardWidth, 60, 0.9f, 0.6f, 0.15f);
        doc.DrawText(x3 + 8, y - 12, "ENERGY DISPENSED", 7, true, 0.7f, 0.4f, 0.05f);
        doc.DrawText(x3 + 8, y - 32, $"{totalUnitsKwh:N2} kWh", 11, true, 0.65f, 0.35f, 0.05f);
        doc.DrawText(x3 + 8, y - 48, "Clean electricity", 6, false, 0.45f, 0.4f, 0.3f);

        // Card 4: Avg Ticket
        decimal avgTicket = totalBookings > 0 ? Math.Round(totalRevenue / totalBookings, 2) : 0m;
        float x4 = x3 + cardWidth + gap;
        doc.FillRect(x4, y - 55, cardWidth, 60, 0.97f, 0.95f, 0.99f);
        doc.DrawRect(x4, y - 55, cardWidth, 60, 0.6f, 0.3f, 0.8f);
        doc.DrawText(x4 + 8, y - 12, "AVG SESSION VALUE", 7, true, 0.5f, 0.2f, 0.7f);
        doc.DrawText(x4 + 8, y - 32, $"Rs. {avgTicket:N2}", 11, true, 0.45f, 0.15f, 0.65f);
        doc.DrawText(x4 + 8, y - 48, "Per vehicle visit", 6, false, 0.45f, 0.35f, 0.5f);

        // ── Transactions Statement Table ──
        y = y - 85;
        doc.DrawText(40, y + 10, "CHARGING TRANSACTIONS AUDIT TRAIL", 10, true, 0.1f, 0.15f, 0.3f);

        // Table Header
        doc.FillRect(40, y - 18, 515, 22, 0.15f, 0.22f, 0.38f);
        doc.DrawText(46, y - 4, "Date", 8, true, 1f, 1f, 1f);
        doc.DrawText(110, y - 4, "Charger / Bay", 8, true, 1f, 1f, 1f);
        doc.DrawText(200, y - 4, "Vehicle / Driver", 8, true, 1f, 1f, 1f);
        doc.DrawText(330, y - 4, "Energy", 8, true, 1f, 1f, 1f);
        doc.DrawText(410, y - 4, "Mode", 8, true, 1f, 1f, 1f);
        doc.DrawText(480, y - 4, "Amount", 8, true, 1f, 1f, 1f);

        float rowY = y - 38;
        int maxRows = Math.Min(bookings.Count, 22);

        for (int i = 0; i < maxRows; i++)
        {
            var b = bookings[i];
            var isAlt = (i % 2 == 1);
            if (isAlt)
            {
                doc.FillRect(40, rowY - 5, 515, 18, 0.97f, 0.98f, 0.99f);
            }
            doc.DrawRect(40, rowY - 5, 515, 18, 0.9f, 0.92f, 0.95f);

            var dateStr = b.BookingDate.ToString("dd MMM yyyy");
            var chargerName = Sanitize(b.Charger?.Name ?? "Charger");
            var vehicle = !string.IsNullOrWhiteSpace(b.VehicleNumberPlate) 
                ? Sanitize(b.VehicleNumberPlate) 
                : Sanitize(b.Customer?.Fullname ?? "Walk-In");
            var energy = $"{(b.UnitsConsumedKwh ?? 0m):F1} kWh";
            var mode = Sanitize(b.PaymentMethod ?? "UPI");
            var amt = $"Rs. {(b.ActualCost ?? b.EstimatedCost):N2}";

            doc.DrawText(46, rowY + 5, dateStr, 7, false, 0.25f, 0.3f, 0.35f);
            doc.DrawText(110, rowY + 5, Truncate(chargerName, 18), 7, true, 0.15f, 0.2f, 0.3f);
            doc.DrawText(200, rowY + 5, Truncate(vehicle, 22), 7, false, 0.25f, 0.3f, 0.35f);
            doc.DrawText(330, rowY + 5, energy, 7, false, 0.1f, 0.45f, 0.2f);
            doc.DrawText(410, rowY + 5, Truncate(mode, 14), 7, false, 0.35f, 0.4f, 0.45f);
            doc.DrawText(480, rowY + 5, amt, 7, true, 0.1f, 0.5f, 0.2f);

            rowY -= 19;
        }

        if (bookings.Count > maxRows)
        {
            doc.DrawText(40, rowY + 4, $"... and {bookings.Count - maxRows} additional transactions recorded in this period.", 7, false, 0.4f, 0.45f, 0.5f);
        }

        // ── Footer ──
        doc.DrawLine(40, 60, 555, 60, 0.85f, 0.88f, 0.92f);
        doc.DrawText(40, 46, "Confidential Financial Record - For Station Owner & Accounting Audits Only.", 7, false, 0.45f, 0.5f, 0.55f);
        doc.DrawText(40, 36, "VoltNet EV Infrastructure Systems India Pvt Ltd | support@voltnet.com", 7, false, 0.45f, 0.5f, 0.55f);
        doc.DrawText(460, 46, $"Generated: {DateTime.UtcNow:dd-MM-yyyy HH:mm} UTC", 7, false, 0.5f, 0.55f, 0.6f);

        return doc.Build();
    }

    private static string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace("(", "").Replace(")", "").Replace("\\", "/").Replace("₹", "Rs. ").Trim();
    }

    private static string Truncate(string input, int maxLen)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Length <= maxLen ? input : input.Substring(0, maxLen - 2) + "..";
    }

    /// <summary>
    /// Lightweight standard PDF 1.4 canvas stream builder.
    /// </summary>
    private class SimplePdfWriter
    {
        private readonly float _pageWidth;
        private readonly float _pageHeight;
        private readonly StringBuilder _stream = new();

        public SimplePdfWriter(float width, float height)
        {
            _pageWidth = width;
            _pageHeight = height;
        }

        public void FillRect(float x, float y, float width, float height, float r, float g, float b)
        {
            _stream.AppendLine("q");
            _stream.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F3} {1:F3} {2:F3} rg", r, g, b));
            _stream.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} re", x, y, width, height));
            _stream.AppendLine("f");
            _stream.AppendLine("Q");
        }

        public void DrawRect(float x, float y, float width, float height, float r, float g, float b, float lineWidth = 0.5f)
        {
            _stream.AppendLine("q");
            _stream.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F2} w", lineWidth));
            _stream.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F3} {1:F3} {2:F3} RG", r, g, b));
            _stream.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} re", x, y, width, height));
            _stream.AppendLine("S");
            _stream.AppendLine("Q");
        }

        public void DrawLine(float x1, float y1, float x2, float y2, float r, float g, float b, float lineWidth = 0.5f)
        {
            _stream.AppendLine("q");
            _stream.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F2} w", lineWidth));
            _stream.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F3} {1:F3} {2:F3} RG", r, g, b));
            _stream.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F2} {1:F2} m", x1, y1));
            _stream.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F2} {1:F2} l", x2, y2));
            _stream.AppendLine("S");
            _stream.AppendLine("Q");
        }

        public void DrawText(float x, float y, string text, float fontSize, bool bold, float r, float g, float b)
        {
            if (string.IsNullOrEmpty(text)) return;
            var fontName = bold ? "/F2" : "/F1";
            var cleanText = EscapePdf(text);

            _stream.AppendLine("BT");
            _stream.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F3} {1:F3} {2:F3} rg", r, g, b));
            _stream.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1:F1} Tf", fontName, fontSize));
            _stream.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F2} {1:F2} Td", x, y));
            _stream.AppendLine($"({cleanText}) Tj");
            _stream.AppendLine("ET");
        }

        private static string EscapePdf(string text)
        {
            var sb = new StringBuilder();
            foreach (var ch in text)
            {
                if (ch == '(') sb.Append("\\(");
                else if (ch == ')') sb.Append("\\)");
                else if (ch == '\\') sb.Append("\\\\");
                else if (ch >= 32 && ch <= 126) sb.Append(ch);
                else sb.Append(' ');
            }
            return sb.ToString();
        }

        public byte[] Build()
        {
            var contentBytes = Encoding.ASCII.GetBytes(_stream.ToString());
            using var ms = new MemoryStream();
            using var writer = new StreamWriter(ms, Encoding.ASCII);

            var offsets = new List<long>();

            // Header
            writer.WriteLine("%PDF-1.4");
            writer.Flush();

            // Obj 1: Catalog
            offsets.Add(ms.Position);
            writer.WriteLine("1 0 obj");
            writer.WriteLine("<< /Type /Catalog /Pages 2 0 R >>");
            writer.WriteLine("endobj");
            writer.Flush();

            // Obj 2: Pages
            offsets.Add(ms.Position);
            writer.WriteLine("2 0 obj");
            writer.WriteLine("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
            writer.WriteLine("endobj");
            writer.Flush();

            // Obj 3: Page
            offsets.Add(ms.Position);
            writer.WriteLine("3 0 obj");
            writer.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {0:F2} {1:F2}] /Contents 4 0 R /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> >>",
                _pageWidth, _pageHeight));
            writer.WriteLine("endobj");
            writer.Flush();

            // Obj 4: Content Stream
            offsets.Add(ms.Position);
            writer.WriteLine("4 0 obj");
            writer.WriteLine($"<< /Length {contentBytes.Length} >>");
            writer.WriteLine("stream");
            writer.Flush();
            ms.Write(contentBytes, 0, contentBytes.Length);
            ms.WriteByte((byte)'\n');
            writer.WriteLine("endstream");
            writer.WriteLine("endobj");
            writer.Flush();

            // Obj 5: Font F1 (Helvetica)
            offsets.Add(ms.Position);
            writer.WriteLine("5 0 obj");
            writer.WriteLine("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
            writer.WriteLine("endobj");
            writer.Flush();

            // Obj 6: Font F2 (Helvetica-Bold)
            offsets.Add(ms.Position);
            writer.WriteLine("6 0 obj");
            writer.WriteLine("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");
            writer.WriteLine("endobj");
            writer.Flush();

            // Xref Table
            var xrefOffset = ms.Position;
            writer.WriteLine("xref");
            writer.WriteLine($"0 {offsets.Count + 1}");
            writer.WriteLine("0000000000 65535 f ");
            foreach (var off in offsets)
            {
                writer.WriteLine($"{off:D10} 00000 n ");
            }

            // Trailer
            writer.WriteLine("trailer");
            writer.WriteLine($"<< /Size {offsets.Count + 1} /Root 1 0 R >>");
            writer.WriteLine("startxref");
            writer.WriteLine(xrefOffset);
            writer.WriteLine("%%EOF");
            writer.Flush();

            return ms.ToArray();
        }
    }
}
