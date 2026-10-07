using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoltNet.Models;

public class StationBlockSlot
{
    public Guid Id { get; set; }

    [Required]
    public Guid StationId { get; set; }

    public Guid? ChargerId { get; set; } // Null means entire station is blocked

    [Required]
    public DateTime BlockDate { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [Required]
    [StringLength(100)]
    public string Reason { get; set; } = "Maintenance"; // Maintenance, Power Cut, Grid Issue, Break Time, Other

    [StringLength(300)]
    public string? Remarks { get; set; }

    public Guid CreatedByManagerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Station? Station { get; set; }
    public virtual Charger? Charger { get; set; }
    public virtual UserMaster? CreatedByManager { get; set; }
}
