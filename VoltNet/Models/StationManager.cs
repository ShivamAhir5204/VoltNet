using System;
using System.ComponentModel.DataAnnotations;

namespace VoltNet.Models;

public class StationManager
{
    public Guid Id { get; set; }

    public Guid StationId { get; set; }

    public Guid UserId { get; set; }

    [Required]
    [StringLength(150)]
    public string FullName { get; set; } = null!;

    [Required]
    [StringLength(20)]
    public string Phone { get; set; } = null!;

    public Guid CreatedBy { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public virtual Station? Station { get; set; }
    public virtual UserMaster? User { get; set; }
    public virtual StationOwner? CreatedByOwner { get; set; }
}
