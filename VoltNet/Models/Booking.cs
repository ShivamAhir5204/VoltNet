using System;
using System.ComponentModel.DataAnnotations;

namespace VoltNet.Models;

public class Booking
{
    public int Id { get; set; }

    public Guid CustomerUserId { get; set; }

    public int ChargerId { get; set; }

    public DateTime BookingTime { get; set; }

    public DateTime SlotStartTime { get; set; }

    public DateTime SlotEndTime { get; set; }

    [Required]
    public string Status { get; set; } = "Booked";

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public virtual UserMaster? CustomerUser { get; set; }
    public virtual Charger? Charger { get; set; }
}
