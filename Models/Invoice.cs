namespace SalesInvoiceAPI.Models;

public class Invoice
{
    public int InvoiceId {get; set; }
    public string InvoiceNumber {get; set; } = string.Empty;
    public int CustomerId {get; set; }
    public DateTime InvoiceDate {get; set;}
    public decimal TotalAmount {get; set;}
}
