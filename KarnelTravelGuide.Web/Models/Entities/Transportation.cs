using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class Transportation
{
    public int TransportationId { get; set; }

    public string? TransportType { get; set; }

    public int? FromBranchId { get; set; }

    public int? ToSpotId { get; set; }

    public DateTime? DepartureTime { get; set; }

    public decimal? PriceTransport { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public virtual Branch? FromBranch { get; set; }

    public virtual ICollection<TicketBooking> TicketBookings { get; set; } = new List<TicketBooking>();

    public virtual TouristSpot? ToSpot { get; set; }
}
