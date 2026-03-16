using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class Transportation
{
    public int TransportationId { get; set; }

    public string TransportType { get; set; } = null!;

    public int DepartureSpotId { get; set; }

    public int DestinationSpotId { get; set; }

    public DateTime DepartureTime { get; set; }

    public decimal PriceTransport { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public virtual TouristSpot DepartureSpot { get; set; } = null!;

    public virtual TouristSpot DestinationSpot { get; set; } = null!;

    public virtual ICollection<TicketBooking> TicketBookings { get; set; } = new List<TicketBooking>();
}
