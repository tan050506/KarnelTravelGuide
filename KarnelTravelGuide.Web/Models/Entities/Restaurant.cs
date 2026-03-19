using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class Restaurant
{
    public int RestaurantId { get; set; }

    public int? SpotId { get; set; }

    public string? RestaurantName { get; set; }

    public string? Address { get; set; }

    public decimal? PriceRes { get; set; }

    public int? StarRating { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<RestaurantBooking> RestaurantBookings { get; set; } = new List<RestaurantBooking>();

    public virtual TouristSpot? Spot { get; set; }
}
