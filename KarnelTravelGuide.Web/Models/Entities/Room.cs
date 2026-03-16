using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class Room
{
    public int RoomId { get; set; }

    public int? HotelId { get; set; }

    public int? ResortId { get; set; }

    public string RoomType { get; set; } = null!;

    public decimal PriceRoom { get; set; }

    public string? ImageUrl { get; set; }

    public virtual Hotel? Hotel { get; set; }

    public virtual Resort? Resort { get; set; }

    public virtual ICollection<RoomBooking> RoomBookings { get; set; } = new List<RoomBooking>();
}
