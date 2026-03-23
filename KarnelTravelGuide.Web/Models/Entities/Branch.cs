using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class Branch
{
    public int BranchId { get; set; }

    public string? BranchName { get; set; }

    public string? Address { get; set; }

    public string? PhoneBranch { get; set; }

    public string? EmailBranch { get; set; }

    public string? ImageUrl { get; set; }

    public virtual ICollection<Account> Accounts { get; set; } = new List<Account>();

    public virtual ICollection<TouristSpot> TouristSpots { get; set; } = new List<TouristSpot>();

    public virtual ICollection<Transportation> Transportations { get; set; } = new List<Transportation>();
}
