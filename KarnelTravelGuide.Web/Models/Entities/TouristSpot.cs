using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class TouristSpot
{
    public int SpotId { get; set; }

    public string SpotName { get; set; } = null!;

    public string? Address { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public virtual ICollection<Hotel> Hotels { get; set; } = new List<Hotel>();

    public virtual ICollection<Resort> Resorts { get; set; } = new List<Resort>();

    public virtual ICollection<Restaurant> Restaurants { get; set; } = new List<Restaurant>();

    public virtual ICollection<Transportation> TransportationDepartureSpots { get; set; } = new List<Transportation>();

    public virtual ICollection<Transportation> TransportationDestinationSpots { get; set; } = new List<Transportation>();
}
