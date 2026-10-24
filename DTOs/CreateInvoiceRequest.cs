namespace SalesInvoiceAPI.DTOs;

public class CreateInvoiceRequest
{
    public int CustomerId { get; set; }
    public decimal DiscountPercent {get; set; } =  0;

    public List<CreateInvoiceItemRequest> Items { get; set; } = new();
    
}

public class CreateInvoiceItemRequest
{
    public int ProductId { get; set; }

    public int Quantity { get; set; }
    public decimal DiscountPercent {get; set; } = 0;
}