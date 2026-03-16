using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class Invoice
{
    public int InvoiceId { get; set; }

    public int AccountId { get; set; }

    public DateTime? CreatedDate { get; set; }

    public decimal SubTotal { get; set; }

    public decimal? DiscountAmount { get; set; }

    public decimal FinalTotal { get; set; }

    public string? PaymentStatus { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual ICollection<RestaurantBooking> RestaurantBookings { get; set; } = new List<RestaurantBooking>();

    public virtual ICollection<RoomBooking> RoomBookings { get; set; } = new List<RoomBooking>();

    public virtual ICollection<TicketBooking> TicketBookings { get; set; } = new List<TicketBooking>();
}
