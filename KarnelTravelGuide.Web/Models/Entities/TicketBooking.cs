using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class TicketBooking
{
    public int TicketId { get; set; }

    public int InvoiceId { get; set; }

    public int TransportationId { get; set; }

    public DateOnly TravelDate { get; set; }

    public decimal TotalAmount { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;

    public virtual Transportation Transportation { get; set; } = null!;
}
