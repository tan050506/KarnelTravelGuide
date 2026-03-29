using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class RestaurantTable
{
    [Key]
    public int TableId { get; set; }

    public int? RestaurantId { get; set; }

    public string? TableType { get; set; }

    public decimal? PriceRes { get; set; } 

    public int Quantity { get; set; }

    public virtual Restaurant? Restaurant { get; set; }

    public virtual ICollection<RestaurantBooking> RestaurantBookings { get; set; } = new List<RestaurantBooking>();
}