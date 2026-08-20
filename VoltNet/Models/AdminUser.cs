using System;
using System.ComponentModel.DataAnnotations;

namespace VoltNet.Models;

public class AdminUser
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = null!;

    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;

    [Required]
    [StringLength(30)]
    public string Role { get; set; } = "Admin"; // "SuperAdmin" or "Admin"

    public bool Isactive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
