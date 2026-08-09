using System;
using System.ComponentModel.DataAnnotations;

namespace VoltNet.Models;

public class MaintenanceTicket
{
    public int Id { get; set; }

    public int ChargerId { get; set; }

    public Guid ReportedByUserId { get; set; }

    [Required]
    public string IssueDescription { get; set; } = null!;

    [Required]
    public string Status { get; set; } = "Open";

    public Guid? AssignedTechnicianId { get; set; }

    public DateTime ReportedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    // Navigation properties
    public virtual Charger? Charger { get; set; }
    public virtual UserMaster? ReportedByUser { get; set; }
    public virtual UserMaster? AssignedTechnician { get; set; }
}
