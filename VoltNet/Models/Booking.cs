using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoltNet.Models;

public class Booking
{
    public Guid Id { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    public Guid StationId { get; set; }

    [Required]
    public Guid ChargerId { get; set; }

    [Required]
    public DateTime BookingDate { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Confirmed"; // Confirmed, Completed, Cancelled, NoShow

    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ActualCost { get; set; }

    // ── Real-World Petrol-Pump Style Meter Billing ──
    [Column(TypeName = "decimal(18,2)")]
    public decimal? UnitsConsumedKwh { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? StartMeterReading { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? EndMeterReading { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? AppliedRatePerKwh { get; set; }

    [StringLength(500)]
    public string? MeterPhotoUrl { get; set; } // Uploaded image proof of energy meter

    // ── Vehicle & Driver Details ──
    [StringLength(30)]
    public string? VehicleNumberPlate { get; set; } // e.g. "GJ-01-EV-1234"

    [StringLength(100)]
    public string? VehicleModel { get; set; } // e.g. "Tata Nexon EV Max"

    [StringLength(20)]
    public string? CustomerPhone { get; set; }

    [StringLength(150)]
    public string? CustomerName { get; set; }

    public bool IsWalkIn { get; set; } = false; // True if created on-spot by manager

    [StringLength(500)]
    public string? CancellationReason { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    [StringLength(100)]
    public string? PaymentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual UserMaster? Customer { get; set; }
    public virtual Station? Station { get; set; }
    public virtual Charger? Charger { get; set; }
}
