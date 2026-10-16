using Microsoft.EntityFrameworkCore;
using SalesInvoiceAPI.Data;
using SalesInvoiceAPI.Models;
using SalesInvoiceAPI.DTOs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api/invoices", () =>
{
    return "Welcome to the Sales Invoices API";
});


app.MapPost("/api/invoices", async (
    CreateInvoiceRequest request,
    AppDbContext db) =>
{
    bool customerExists = await db.Customers
        .AnyAsync(c => c.CustomerId == request.CustomerId);

    if (!customerExists)
    {
        return Results.BadRequest(
            "The selected customer does not exist."
        );
    }

    if (request.Items == null || request.Items.Count == 0)
    {
        return Results.BadRequest(
            "An invoice must contain at least one item"
        );
    }

    foreach (var item in request.Items)
    {
        if (item.Quantity <= 0)
        {
            return Results.BadRequest(
                "Product quantity must be greater than zro."
            );
        }
    }

    foreach (var item in request.Items)
    {
        bool productExists = await db.Products
            .AnyAsync(p => p.ProductId == item.ProductId);
        
        if (!productExists)
        {
            return Results.BadRequest(
                $"Product with ID {item.ProductId} does not exist."
            );
        }
    }

    
    var productIds = request.Items
        .Select(item => item.ProductId)
        .Distinct()
        .ToList();

    var products = await db.Products
        .Where(p => productIds.Contains(p.ProductId))
        .ToListAsync(); 

    
    decimal invoiceTotal = 0;
    foreach (var item in request.Items)
    {
        var product = products.First(
            p => p.ProductId == item.ProductId
        );

        decimal lineTotal = product.Price * item.Quantity;
        invoiceTotal += lineTotal;
    }
    await using var transaction = await db.Database.BeginTransactionAsync();

    var invoice = new Invoice
    {
        CustomerId = request.CustomerId,
        InvoiceDate = DateTime.UtcNow,
        TotalAmount = invoiceTotal,
        InvoiceNumber = $"TEMP-{Guid.NewGuid():N}"[..30]
    };

    db.Invoices.Add(invoice);
    await db.SaveChangesAsync();

    invoice.InvoiceNumber = $"INV-{invoice.InvoiceId:D6}";
    await db.SaveChangesAsync();


    foreach (var item in request.Items)
    {
        var product = products.First(
            p => p.ProductId == item.ProductId
        );

        var invoiceItem = new InvoiceItem
        {
            InvoiceId = invoice.InvoiceId,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            UnitPrice = product.Price,
            LineTotal = product.Price * item.Quantity
        };

        db.InvoiceItems.Add(invoiceItem);

    }

    await db.SaveChangesAsync(); // --> this saves all the invoice items
    await transaction.CommitAsync(); // --> and thiss commits the transactions


    return Results.Created($"/api/invoices/{invoice.InvoiceId}" , new
    {
        Message = "Invoice created successfully.",
        InvoiceId = invoice.InvoiceId,
        InvoiceNumber = invoice.InvoiceNumber,
        TotalAmount = invoiceTotal
    }
        
    );
});

app.MapGet("/api/invoices/{id:int}", async (
    int id,
    AppDbContext db) =>
{
    var invoice = await db.Invoices
        .Where(i => i.InvoiceId == id)
        .Select(i => new
        {
            i.InvoiceId,
            i.InvoiceNumber,
            i.InvoiceDate,
            i.CustomerId,
            CustomerName = db.Customers
                .Where(c => c.CustomerId == i.CustomerId)
                .Select(c => c.Name)
                .FirstOrDefault(),

            i.TotalAmount,
            Items = db.InvoiceItems
                .Where(item => item.InvoiceId == i.InvoiceId)
                .Select(item => new
                {
                    item.ProductId,
                    ProductName = db.Products
                        .Where(p => p.ProductId == item.ProductId)
                        .Select(p => p.Name)
                        .FirstOrDefault(),
                    item.Quantity,
                    item.UnitPrice,
                    item.LineTotal
                })
                .ToList()
        })
        .FirstOrDefaultAsync();

    if (invoice == null)
    {
        return Results.NotFound("Invoice not found.");
    }

    return Results.Ok(invoice);
});




app.MapGet("/api/reports/sales", async (
    DateTime? fromDate,
    DateTime? toDate,
    int? customerId,
    AppDbContext db) =>
{
    
    if (fromDate.HasValue && toDate.HasValue &&
        fromDate.Value > toDate.Value)
    {
        return Results.BadRequest(
            "FromDate cannot be later than ToDate."
        );
    }

   
    var query = db.Invoices.AsQueryable();

    
    if (fromDate.HasValue)
    {
        query = query.Where(i =>
            i.InvoiceDate >= fromDate.Value);
    }

    
    if (toDate.HasValue)
    {
        if (toDate.Value.Date == DateTime.MaxValue.Date)
        {
            query = query.Where(i =>
                i.InvoiceDate <= toDate.Value);
        }
        else
        {
            DateTime endDateExclusive = toDate.Value.Date.AddDays(1);

            query = query.Where(i =>
                i.InvoiceDate < endDateExclusive);
        }
    }

    
    if (customerId.HasValue)
    {
        query = query.Where(i =>
            i.CustomerId == customerId.Value);
    }

    
    var transactions = await query
        .OrderByDescending(i => i.InvoiceDate)
        .Select(i => new
        {
            i.InvoiceNumber,
            i.InvoiceDate,
            CustomerId = i.CustomerId,
            CustomerName = db.Customers
                .Where(c => c.CustomerId == i.CustomerId)
                .Select(c => c.Name)
                .FirstOrDefault(),
            i.TotalAmount
        })
        .ToListAsync();

    
    decimal totalSalesAmount = transactions
        .Sum(t => t.TotalAmount);

    int totalInvoices = transactions.Count;

    return Results.Ok(new
    {
        Transactions = transactions,
        TotalSalesAmount = totalSalesAmount,
        TotalInvoices = totalInvoices
    });
});








app.Run();
