using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class Stay
{
    public int StayId { get; set; }

    public int? SpotId { get; set; }

    public string? Name { get; set; }

    public string? Address { get; set; }

    public int? StarRating { get; set; }

    public string? StayType { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();

    public virtual TouristSpot? Spot { get; set; }
}
