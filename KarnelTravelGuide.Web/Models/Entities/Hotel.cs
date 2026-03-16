using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class Hotel
{
    public int HotelId { get; set; }

    public int SpotId { get; set; }

    public string HotelName { get; set; } = null!;

    public string Address { get; set; } = null!;

    public int? StarRating { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();

    public virtual TouristSpot Spot { get; set; } = null!;
}
