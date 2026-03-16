using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class RestaurantBooking
{
    public int BookingId { get; set; }

    public int InvoiceId { get; set; }

    public int RestaurantId { get; set; }

    public DateTime ReservationDateTime { get; set; }

    public int NumberOfGuests { get; set; }

    public string? SpecialRequest { get; set; }

    public decimal? TotalAmount { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;

    public virtual Restaurant Restaurant { get; set; } = null!;
}
