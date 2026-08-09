using System;
using System.ComponentModel.DataAnnotations;

namespace VoltNet.Models;

public class WalletTransaction
{
    public int Id { get; set; }

    public int WalletId { get; set; }

    public decimal Amount { get; set; }

    [Required]
    public string Type { get; set; } = null!;

    public DateTime Timestamp { get; set; }

    // Navigation properties
    public virtual Wallet? Wallet { get; set; }
}
