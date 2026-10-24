namespace SalesInvoiceAPI.DTOs;

public class CreateCustomerRequest
{
    public string Name {get; set; } = "";
    public string Email {get; set;} = "";
    public string Phone {get; set;} = "";
}
