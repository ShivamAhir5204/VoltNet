using System;
using System.ComponentModel.DataAnnotations;

namespace VoltNet.Models;

public class Charger
{
    public Guid Id { get; set; }

    [Required]
    public Guid StationId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = null!; // e.g. "Charger 1", "Fast Charger A"

    [Required]
    [StringLength(50)]
    public string ConnectorType { get; set; } = null!; // CCS2, CHAdeMO, Type 2, etc.

    [Required]
    [Range(1, 500)]
    public double CapacityKw { get; set; } // e.g. 50.0, 150.0

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Active"; // Active, Maintenance, Offline

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual Station? Station { get; set; }
}
