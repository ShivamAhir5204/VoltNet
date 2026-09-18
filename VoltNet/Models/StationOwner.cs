using System;
using System.ComponentModel.DataAnnotations;

namespace VoltNet.Models;

public class StationOwner
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [Required]
    [StringLength(150)]
    public string FullName { get; set; } = null!;

    [StringLength(150)]
    public string? BusinessName { get; set; }

    [Required]
    [StringLength(20)]
    public string Phone { get; set; } = null!;

    [StringLength(100)]
    public string? BusinessRegistrationNumber { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(20)]
    public string? GSTNumber { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public virtual UserMaster User { get; set; } = null!;
    public virtual UserMaster? ApprovedByUser { get; set; }
}
