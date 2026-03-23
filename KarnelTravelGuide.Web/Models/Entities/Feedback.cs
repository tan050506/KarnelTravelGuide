using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class Feedback
{
    public int FeedbackId { get; set; }

    public int? AccountId { get; set; }

    public int? StayId { get; set; }

    public int? RestaurantId { get; set; }

    public string? Message { get; set; }

    public string? ReplyMessage { get; set; }

    public virtual Account? Account { get; set; }

    public virtual Restaurant? Restaurant { get; set; }

    public virtual Stay? Stay { get; set; }
}
