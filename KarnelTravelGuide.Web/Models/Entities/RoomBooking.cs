using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class RoomBooking
{
    public int RoomBookingId { get; set; }

    public int? RoomId { get; set; }

    public DateOnly? CheckInDate { get; set; }

    public DateOnly? CheckOutDate { get; set; }

    public decimal? TotalAmount { get; set; }

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual Room? Room { get; set; }
}
