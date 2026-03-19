using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class Account
{
    public int AccountId { get; set; }

    public string FullName { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    public string? Role { get; set; }

    public int? BranchId { get; set; }

    public string? AvatarUrl { get; set; }

    public virtual Branch? Branch { get; set; }

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
