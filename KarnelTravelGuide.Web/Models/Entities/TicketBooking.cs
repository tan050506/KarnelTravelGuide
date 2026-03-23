using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class TicketBooking
{
    public int TicketBookingId { get; set; }

    public int? TransportationId { get; set; }

    public DateOnly? TravelDate { get; set; }

    public string? Seat { get; set; }

    public decimal? TotalAmount { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual Transportation? Transportation { get; set; }
}
