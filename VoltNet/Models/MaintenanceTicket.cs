using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoltNet.Models;

public class MaintenanceTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid StationId { get; set; }

    public Guid? ChargerId { get; set; }

    [Required]
    public Guid ReportedById { get; set; }

    [Required]
    [StringLength(150)]
    public string Title { get; set; } = null!;

    [Required]
    [StringLength(1000)]
    public string Description { get; set; } = null!;

    [Required]
    [StringLength(30)]
    public string Priority { get; set; } = "Medium"; // Low, Medium, High, Critical

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Open"; // Open, InProgress, Resolved, Closed

    [StringLength(1000)]
    public string? ResolutionNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    // Navigation
    public virtual Station? Station { get; set; }
    public virtual Charger? Charger { get; set; }
    public virtual UserMaster? ReportedBy { get; set; }
}
