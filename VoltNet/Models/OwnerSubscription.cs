using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VoltNet.Models;

public class OwnerSubscription
{
    public Guid Id { get; set; }

    public Guid StationOwnerId { get; set; }

    public Guid PlanId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Active"; // Active, CancellationPending, Cancelled, Expired

    [StringLength(100)]
    public string? PaymentId { get; set; }

    public decimal AmountPaid { get; set; }

    [StringLength(500)]
    public string? CancellationReason { get; set; }

    public DateTime? CancellationRequestedAt { get; set; }

    public DateTime? CancellationProcessedAt { get; set; }

    [StringLength(500)]
    public string? CancellationAdminRemarks { get; set; }

    public decimal? RefundAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual StationOwner? Owner { get; set; }
    public virtual SubscriptionPlan? Plan { get; set; }
    
    public virtual ICollection<Station> Stations { get; set; } = new List<Station>();
}
