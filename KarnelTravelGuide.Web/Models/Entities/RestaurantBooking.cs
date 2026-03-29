using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class RestaurantBooking
{
    public int ResBookingId { get; set; }

    public int? TableId { get; set; } 

    public DateTime? ReservationDateTime { get; set; }

    public int? NumberOfGuests { get; set; }

    public string? SpecialRequest { get; set; }

    public decimal? TotalAmount { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual RestaurantTable? RestaurantTable { get; set; } 
}