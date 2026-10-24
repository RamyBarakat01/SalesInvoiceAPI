
namespace SalesInvoiceAPI.DTOs;

public class UpdateInvoiceRequest
{
    public decimal? DiscountPercent {get; set; }
    public List<UpdateInvoiceItemRequest> Items { get; set; } = new();
}

public class UpdateInvoiceItemRequest
{
    public int InvoiceItemId { get; set; }
    public int? ProductId {get; set; }
    public int? Quantity {get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? DiscountPercent {get; set; }
}
