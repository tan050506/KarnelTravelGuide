using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class RoomBooking
{
    public int BookingId { get; set; }

    public int InvoiceId { get; set; }

    public int RoomId { get; set; }

    public DateOnly CheckInDate { get; set; }

    public DateOnly CheckOutDate { get; set; }

    public decimal TotalAmount { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;

    public virtual Room Room { get; set; } = null!;
}
