using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class Branch
{
    public int BranchId { get; set; }

    public string BranchName { get; set; } = null!;

    public string Address { get; set; } = null!;

    public string? EmailBranch { get; set; }

    public string? PhoneBranch { get; set; }

    public virtual ICollection<Account> Accounts { get; set; } = new List<Account>();
}
