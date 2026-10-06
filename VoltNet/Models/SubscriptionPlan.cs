using System;
using System.ComponentModel.DataAnnotations;

namespace VoltNet.Models;

public class SubscriptionPlan
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = null!;

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = null!;

    [Required]
    public decimal PricePerMonth { get; set; }

    [Required]
    public int MaxStations { get; set; }

    [Required]
    public int MaxManagersPerStation { get; set; }

    [Required]
    public int DurationDays { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
