using System;

namespace VoltNet.Models;

public class Wallet
{
    public int Id { get; set; }

    public Guid CustomerUserId { get; set; }

    public decimal Balance { get; set; }

    public DateTime LastUpdated { get; set; }

    // Navigation properties
    public virtual UserMaster? CustomerUser { get; set; }
}
