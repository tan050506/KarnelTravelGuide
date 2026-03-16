using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class Feedback
{
    public int FeedbackId { get; set; }

    public int? AccountId { get; set; }

    public int? HotelId { get; set; }

    public int? ResortId { get; set; }

    public int? RestaurantId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public int? StarRating { get; set; }

    public string Message { get; set; } = null!;

    public string? ReplyMessage { get; set; }

    public virtual Account? Account { get; set; }

    public virtual Hotel? Hotel { get; set; }

    public virtual Resort? Resort { get; set; }

    public virtual Restaurant? Restaurant { get; set; }
}
