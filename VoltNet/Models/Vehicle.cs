using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoltNet.Models;

public class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    [StringLength(30)]
    public string VehicleNumberPlate { get; set; } = null!;

    [Required]
    [StringLength(50)]
    public string Brand { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string Model { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BatteryCapacityKwh { get; set; }

    [Required]
    [StringLength(50)]
    public string ConnectorType { get; set; } = "CCS2";

    public bool IsDefault { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual UserMaster? Customer { get; set; }
}
