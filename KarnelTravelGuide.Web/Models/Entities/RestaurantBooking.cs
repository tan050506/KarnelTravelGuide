using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class RestaurantBooking
{
    public int ResBookingId { get; set; }

    public int? RestaurantId { get; set; }

    public DateTime? ReservationDateTime { get; set; }

    public int? NumberOfGuests { get; set; }

    public string? SpecialRequest { get; set; }

    public decimal? TotalAmount { get; set; }

    public string? TableType { get; set; }

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual Restaurant? Restaurant { get; set; }
}
