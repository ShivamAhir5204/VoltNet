using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoltNet.Models;

public class WaitlistEntry
{
    public Guid Id { get; set; }

    [Required]
    public Guid StationId { get; set; }

    [Required]
    public Guid ChargerId { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    public DateTime BookingDate { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [Required]
    public int QueuePosition { get; set; } = 1;

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Waiting"; // Waiting, Promoted, Cancelled, Expired

    [StringLength(50)]
    public string? VehicleNumberPlate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PromotedAt { get; set; }

    // Navigation properties
    public virtual Station? Station { get; set; }
    public virtual Charger? Charger { get; set; }
    public virtual UserMaster? Customer { get; set; }
}
