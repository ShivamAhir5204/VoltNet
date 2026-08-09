using System;
using System.ComponentModel.DataAnnotations;

namespace VoltNet.Models;

public class Charger
{
    public int Id { get; set; }

    public int StationId { get; set; }

    [Required]
    public string ChargerCode { get; set; } = null!;

    [Required]
    public string Type { get; set; } = null!;

    [Required]
    public string ConnectorType { get; set; } = null!;

    public decimal PowerRatingKw { get; set; }

    public decimal RatePerKwh { get; set; }

    [Required]
    public string Status { get; set; } = "Available";

    public DateTime? LastServicedAt { get; set; }

    // Navigation properties
    public virtual Station? Station { get; set; }
}
