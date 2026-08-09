using System;
using System.ComponentModel.DataAnnotations;

namespace VoltNet.Models;

public class Invoice
{
    public int Id { get; set; }

    public Guid CustomerUserId { get; set; }

    public int ChargingSessionId { get; set; }

    public decimal Amount { get; set; }

    public decimal GSTAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public DateTime IssuedDate { get; set; }

    [Required]
    public string PaymentMethod { get; set; } = null!;

    // Navigation properties
    public virtual UserMaster? CustomerUser { get; set; }
    public virtual ChargingSession? ChargingSession { get; set; }
}
