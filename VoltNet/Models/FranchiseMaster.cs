using System;
using System.ComponentModel.DataAnnotations;

namespace VoltNet.Models;

public partial class FranchiseMaster
{
    public Guid Id { get; set; }

    public Guid OwnerUserId { get; set; }

    [Required]
    public string FranchiseName { get; set; } = null!;

    public decimal RevenueSharePercent { get; set; }

    public string? ContactEmail { get; set; }

    public string? Address { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation property
    public virtual UserMaster? OwnerUser { get; set; }
}
