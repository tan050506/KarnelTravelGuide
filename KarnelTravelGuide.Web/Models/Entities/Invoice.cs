using System;
using System.Collections.Generic;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class Invoice
{
    public int InvoiceId { get; set; }

    public int? AccountId { get; set; }

    public int? OrderId { get; set; }

    public DateTime? CreatedDate { get; set; }

    public decimal? SubTotal { get; set; }

    public decimal? DiscountAmount { get; set; }

    public decimal? FinalTotal { get; set; }

    public string? PaymentStatus { get; set; }

    public virtual Account? Account { get; set; }

    public virtual Order? Order { get; set; }
}
