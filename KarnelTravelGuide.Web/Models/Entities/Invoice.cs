using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class Invoice
{
    public int InvoiceId { get; set; }

    public int? AccountId { get; set; }

    public DateTime? CreatedDate { get; set; }

    public decimal? SubTotal { get; set; }

    public decimal? DiscountAmount { get; set; }

    public int? RoomBookingId { get; set; }

    public int? ResBookingId { get; set; }

    public int? TicketBookingId { get; set; }

    public decimal? FinalTotal { get; set; }

    public string? PaymentStatus { get; set; }

    public virtual Account? Account { get; set; }

    public virtual RestaurantBooking? ResBooking { get; set; }

    public virtual RoomBooking? RoomBooking { get; set; }

    public virtual TicketBooking? TicketBooking { get; set; }
}
