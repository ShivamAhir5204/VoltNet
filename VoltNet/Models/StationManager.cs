using System;

namespace VoltNet.Models;

public class StationManager
{
    public Guid Id { get; set; }

    public Guid StationId { get; set; }

    public Guid UserId { get; set; }

    public DateTime AssignedAt { get; set; }

    // Navigation properties
    public virtual Station? Station { get; set; }
    public virtual UserMaster? User { get; set; }
}
