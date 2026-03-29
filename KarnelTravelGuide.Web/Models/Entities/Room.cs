using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class Room
{
    public int RoomId { get; set; }

    public int? StayId { get; set; }

    public string? RoomType { get; set; }

    public decimal? PriceRoom { get; set; }

    public int Quantity { get; set; }

    public virtual ICollection<RoomBooking> RoomBookings { get; set; } = new List<RoomBooking>();

    public virtual Stay? Stay { get; set; }
}
