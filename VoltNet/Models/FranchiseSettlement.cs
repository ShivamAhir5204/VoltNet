using System;
using System.ComponentModel.DataAnnotations;

namespace VoltNet.Models;

public class FranchiseSettlement
{
    public int Id { get; set; }

    public Guid FranchiseId { get; set; }

    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEnd { get; set; }

    public decimal TotalRevenue { get; set; }

    public decimal FranchiseShareAmount { get; set; }

    public decimal CompanyShareAmount { get; set; }

    [Required]
    public string SettlementStatus { get; set; } = "Pending";

    // Navigation properties
    public virtual FranchiseMaster? Franchise { get; set; }
}
