using System;
using System.ComponentModel.DataAnnotations;

namespace VoltNet.Models;

public class Station
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = null!;

    public Guid? FranchiseId { get; set; }

    [Required]
    public string Address { get; set; } = null!;

    [Required]
    public string City { get; set; } = null!;

    [Required]
    public string State { get; set; } = null!;

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public TimeSpan OpeningTime { get; set; }

    public TimeSpan ClosingTime { get; set; }

    [Required]
    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public virtual FranchiseMaster? Franchise { get; set; }
}
