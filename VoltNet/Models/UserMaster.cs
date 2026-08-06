using System;
using System.Collections.Generic;

namespace VoltNet.Models;

public partial class UserMaster
{
    public Guid Id { get; set; }

    public string? Fullname { get; set; }

    public string Email { get; set; } = null!;

    public string? Password { get; set; }

    public string? Mobile { get; set; }

    public string Role { get; set; } = null!;

    public bool Isactive { get; set; }

    public DateTime CreatedAt { get; set; }
}
