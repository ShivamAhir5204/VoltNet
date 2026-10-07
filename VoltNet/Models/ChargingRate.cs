using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoltNet.Models;

public class ChargingRate
{
    public Guid Id { get; set; }

    [Required]
    public Guid StationId { get; set; }

    [Required]
    [StringLength(50)]
    public string ConnectorType { get; set; } = null!; // e.g., CCS2, Type 2, CHAdeMO, GB/T

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    [Range(0.01, 150.00, ErrorMessage = "Rate per kWh must be between ₹0.01 and ₹150.00")]
    public decimal RatePerKwh { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Range(0.00, 2000.00, ErrorMessage = "Rate per hour must be between ₹0.00 and ₹2000.00")]
    public decimal? RatePerHour { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    public virtual Station? Station { get; set; }
}
