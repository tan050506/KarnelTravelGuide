using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class OrderDetail
{
    public int OrderDetailId { get; set; }

    public int? OrderId { get; set; }

    public int? RoomBookingId { get; set; }

    public int? TicketBookingId { get; set; }

    public int? ResBookingId { get; set; }

    public decimal? Price { get; set; }

    public int? Quantity { get; set; }

    public virtual Order? Order { get; set; }

    public virtual RestaurantBooking? ResBooking { get; set; }

    public virtual RoomBooking? RoomBooking { get; set; }

    public virtual TicketBooking? TicketBooking { get; set; }
}
