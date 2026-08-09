using System;
using System.ComponentModel.DataAnnotations;

namespace VoltNet.Models;

public class ChargingSession
{
    public int Id { get; set; }

    public int BookingId { get; set; }

    public DateTime ActualStartTime { get; set; }

    public DateTime? ActualEndTime { get; set; }

    public decimal? EnergyConsumedKwh { get; set; }

    public decimal? Cost { get; set; }

    [Required]
    public string PaymentStatus { get; set; } = "Pending";

    // Navigation properties
    public virtual Booking? Booking { get; set; }
}
