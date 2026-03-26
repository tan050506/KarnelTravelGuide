using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class TouristSpot
{
    public int SpotId { get; set; }

    public string? SpotName { get; set; }

    public string? Address { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public int? BranchId { get; set; }

    public virtual Branch? Branch { get; set; }

    public virtual ICollection<Restaurant> Restaurants { get; set; } = new List<Restaurant>();

    public virtual ICollection<Stay> Stays { get; set; } = new List<Stay>();

    public virtual ICollection<Transportation> Transportations { get; set; } = new List<Transportation>();

    public virtual ICollection<TouristSpotImage> TouristSpotImages { get; set; } = new List<TouristSpotImage>();
}
