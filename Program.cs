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

// Basic API welcome endpoint
app.MapGet("/api/invoices", () =>
{
    return "Welcome to the Sales Invoices API";
});



// CREATE AN INVOICE (ADDED DISCOUNTS)
app.MapPost("/api/invoices", async (
    CreateInvoiceRequest request,
    AppDbContext db) =>
{
    bool customerExists = await db.Customers
        .AnyAsync(c => c.CustomerId == request.CustomerId);

    if (!customerExists)
    {
        return Results.BadRequest(
            "The selected customer does not exist.");
    }

    if (request.Items == null || request.Items.Count == 0)
    {
        return Results.BadRequest(
            "An invoice must contain at least one item.");
    }

    if (request.DiscountPercent < 0 ||
        request.DiscountPercent > 100 ||
        decimal.Round(request.DiscountPercent, 2) != request.DiscountPercent)
    {
        return Results.BadRequest(
            "Invoice discount must be between 0 and 100 with at most two decimal places.");
    }

    foreach (var item in request.Items)
    {
        if (item.Quantity <= 0)
        {
            return Results.BadRequest(
                "Product quantity must be greater than zero.");
        }

        if (item.DiscountPercent < 0 ||
            item.DiscountPercent > 100 ||
            decimal.Round(item.DiscountPercent, 2) != item.DiscountPercent)
        {
            return Results.BadRequest(
                "Item discount must be between 0 and 100 with at most two decimal places.");
        }
    }

    var productIds = request.Items
        .Select(item => item.ProductId)
        .Distinct()
        .ToList();

    var products = await db.Products
        .Where(p => productIds.Contains(p.ProductId))
        .ToDictionaryAsync(p => p.ProductId);

    foreach (var item in request.Items)
    {
        if (!products.ContainsKey(item.ProductId))
        {
            return Results.BadRequest(
                $"Product with ID {item.ProductId} does not exist.");
        }
    }

    // Calculate the subtotal after individual item discounts.
    decimal subtotal = 0;

    foreach (var item in request.Items)
    {
        var product = products[item.ProductId];

        decimal originalLineTotal =
            product.Price * item.Quantity;

        decimal itemDiscountAmount = decimal.Round(
            originalLineTotal * item.DiscountPercent / 100,
            2,
            MidpointRounding.AwayFromZero);

        decimal discountedLineTotal =
            originalLineTotal - itemDiscountAmount;

        subtotal += discountedLineTotal;
    }

    // Apply the invoice discount after item discounts.
    decimal invoiceDiscountAmount = decimal.Round(
        subtotal * request.DiscountPercent / 100,
        2,
        MidpointRounding.AwayFromZero);

    // Round down to a whole EGP and record the removed piasters.
    decimal totalBeforeRounding = subtotal - invoiceDiscountAmount;
    decimal finalTotal = decimal.Floor(totalBeforeRounding);
    decimal roundingDiscount = totalBeforeRounding - finalTotal;

    await using var transaction =
        await db.Database.BeginTransactionAsync();

    var invoice = new Invoice
    {
        CustomerId = request.CustomerId,
        InvoiceDate = DateTime.UtcNow,
        InvoiceNumber = $"TEMP-{Guid.NewGuid():N}"[..30],
        DiscountPercent = request.DiscountPercent,
        RoundingDiscount = roundingDiscount,
        TotalAmount = finalTotal
    };

    db.Invoices.Add(invoice);
    await db.SaveChangesAsync();

    invoice.InvoiceNumber = $"INV-{invoice.InvoiceId:D6}";
    await db.SaveChangesAsync();

    foreach (var item in request.Items)
    {
        var product = products[item.ProductId];

        decimal originalLineTotal =
            product.Price * item.Quantity;

        decimal itemDiscountAmount = decimal.Round(
            originalLineTotal * item.DiscountPercent / 100,
            2,
            MidpointRounding.AwayFromZero);

        decimal discountedLineTotal =
            originalLineTotal - itemDiscountAmount;

        db.InvoiceItems.Add(new InvoiceItem
        {
            InvoiceId = invoice.InvoiceId,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            UnitPrice = product.Price,
            DiscountPercent = item.DiscountPercent,
            LineTotal = discountedLineTotal
        });
    }

    await db.SaveChangesAsync();
    await transaction.CommitAsync();

    return Results.Created(
        $"/api/invoices/{invoice.InvoiceId}",
        new
        {
            Message = "Invoice created successfully.",
            invoice.InvoiceId,
            invoice.InvoiceNumber,
            invoice.DiscountPercent,
            Subtotal = subtotal,
            InvoiceDiscountAmount = invoiceDiscountAmount,
            invoice.RoundingDiscount,
            invoice.TotalAmount
        });
});



// GET INVOICE DETAILS
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

            i.DiscountPercent,
            i.RoundingDiscount,
            i.TotalAmount,

            Items = db.InvoiceItems
                .Where(item => item.InvoiceId == i.InvoiceId)
                .Select(item => new
                {
                    item.InvoiceItemId,
                    item.ProductId,

                    ProductName = db.Products
                        .Where(p => p.ProductId == item.ProductId)
                        .Select(p => p.Name)
                        .FirstOrDefault(),

                    item.Quantity,
                    item.UnitPrice,
                    item.DiscountPercent,
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





// UPDATE INVOICE ITEMS, QUANTITIES, PRICES AND DISCOUNTS
app.MapPut("/api/invoices/{id:int}", async (
    int id,
    UpdateInvoiceRequest request,
    AppDbContext db) =>
{
    var invoice = await db.Invoices
        .FirstOrDefaultAsync(i => i.InvoiceId == id);

    if (invoice == null)
    {
        return Results.NotFound("Invoice not found.");
    }

    if (request.Items == null || request.Items.Count == 0)
    {
        return Results.BadRequest(
            "An invoice must contain at least one item.");
    }

    // If omitted, keep the saved invoice discount.
    decimal invoiceDiscountPercent =
        request.DiscountPercent ?? invoice.DiscountPercent;

    if (invoiceDiscountPercent < 0 ||
        invoiceDiscountPercent > 100 ||
        decimal.Round(invoiceDiscountPercent, 2) != invoiceDiscountPercent)
    {
        return Results.BadRequest(
            "Invoice discount must be between 0 and 100 with at most two decimal places.");
    }

    var existingItems = await db.InvoiceItems
        .Where(i => i.InvoiceId == id)
        .ToListAsync();

    var newProductIds = request.Items
        .Where(i => i.InvoiceItemId == 0 && i.ProductId.HasValue)
        .Select(i => i.ProductId!.Value)
        .Distinct()
        .ToList();

    var products = await db.Products
        .Where(p => newProductIds.Contains(p.ProductId))
        .ToDictionaryAsync(p => p.ProductId);

    var seenItemIds = new HashSet<int>();
    var seenProductIds = new HashSet<int>();

    var updatedItems = new List<(
        InvoiceItem? Existing,
        int ProductId,
        int Quantity,
        decimal UnitPrice,
        decimal DiscountPercent)>();

    // Validate all items before making database changes.
    foreach (var item in request.Items)
    {
        if (item.InvoiceItemId < 0)
        {
            return Results.BadRequest("Invalid invoice item ID.");
        }

        InvoiceItem? existingItem = null;

        int productId;
        int quantity;
        decimal unitPrice;
        decimal discountPercent;

        if (item.InvoiceItemId > 0)
        {
            if (!seenItemIds.Add(item.InvoiceItemId))
            {
                return Results.BadRequest(
                    "Duplicate invoice item ID.");
            }

            existingItem = existingItems.FirstOrDefault(
                i => i.InvoiceItemId == item.InvoiceItemId);

            if (existingItem == null)
            {
                return Results.BadRequest(
                    "Invoice item does not belong to this invoice.");
            }

            productId = existingItem.ProductId;

            if (item.ProductId.HasValue &&
                item.ProductId.Value != productId)
            {
                return Results.BadRequest(
                    "Cannot change the product of an existing item.");
            }

            quantity = item.Quantity ?? existingItem.Quantity;
            unitPrice = item.UnitPrice ?? existingItem.UnitPrice;
            discountPercent =
                item.DiscountPercent ?? existingItem.DiscountPercent;
        }
        else
        {
            if (!item.ProductId.HasValue ||
                !item.Quantity.HasValue)
            {
                return Results.BadRequest(
                    "New items require a product ID and quantity.");
            }

            productId = item.ProductId.Value;
            quantity = item.Quantity.Value;

            if (!products.TryGetValue(productId, out var product))
            {
                return Results.BadRequest(
                    $"Product with ID {productId} does not exist.");
            }

            // New items always use the catalog price.
            if (item.UnitPrice.HasValue)
            {
                return Results.BadRequest(
                    "Do not supply a price for new items.");
            }

            unitPrice = product.Price;

            // New items default to 0% discount.
            discountPercent = item.DiscountPercent ?? 0;
        }

        if (quantity <= 0)
        {
            return Results.BadRequest(
                "Quantity must be greater than zero.");
        }

        if (unitPrice < 0 ||
            decimal.Round(unitPrice, 2) != unitPrice)
        {
            return Results.BadRequest(
                "Unit price must be non-negative with at most two decimal places.");
        }

        if (discountPercent < 0 ||
            discountPercent > 100 ||
            decimal.Round(discountPercent, 2) != discountPercent)
        {
            return Results.BadRequest(
                "Item discount must be between 0 and 100 with at most two decimal places.");
        }

        if (!seenProductIds.Add(productId))
        {
            return Results.BadRequest(
                "The same product cannot appear twice.");
        }

        updatedItems.Add((
            existingItem,
            productId,
            quantity,
            unitPrice,
            discountPercent));
    }

    await using var transaction =
        await db.Database.BeginTransactionAsync();

    // Remove existing items omitted from the request.
    var itemsToRemove = existingItems
        .Where(i => !seenItemIds.Contains(i.InvoiceItemId))
        .ToList();

    db.InvoiceItems.RemoveRange(itemsToRemove);

    decimal subtotal = 0;

    foreach (var item in updatedItems)
    {
        decimal originalLineTotal =
            item.UnitPrice * item.Quantity;

        decimal itemDiscountAmount = decimal.Round(
            originalLineTotal * item.DiscountPercent / 100,
            2,
            MidpointRounding.AwayFromZero);

        decimal discountedLineTotal =
            originalLineTotal - itemDiscountAmount;

        if (item.Existing != null)
        {
            item.Existing.Quantity = item.Quantity;
            item.Existing.UnitPrice = item.UnitPrice;
            item.Existing.DiscountPercent = item.DiscountPercent;
            item.Existing.LineTotal = discountedLineTotal;
        }
        else
        {
            db.InvoiceItems.Add(new InvoiceItem
            {
                InvoiceId = id,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountPercent = item.DiscountPercent,
                LineTotal = discountedLineTotal
            });
        }

        subtotal += discountedLineTotal;
    }

    decimal invoiceDiscountAmount = decimal.Round(
        subtotal * invoiceDiscountPercent / 100,
        2,
        MidpointRounding.AwayFromZero);

    // Round down to a whole EGP after all percentage discounts.
    decimal totalBeforeRounding = subtotal - invoiceDiscountAmount;
    decimal finalTotal = decimal.Floor(totalBeforeRounding);
    decimal roundingDiscount = totalBeforeRounding - finalTotal;

    invoice.DiscountPercent = invoiceDiscountPercent;
    invoice.RoundingDiscount = roundingDiscount;
    invoice.TotalAmount = finalTotal;

    await db.SaveChangesAsync();
    await transaction.CommitAsync();

    return Results.Ok(new
    {
        Message = "Invoice updated successfully.",
        invoice.InvoiceId,
        invoice.InvoiceNumber,
        invoice.DiscountPercent,
        Subtotal = subtotal,
        InvoiceDiscountAmount = invoiceDiscountAmount,
        invoice.RoundingDiscount,
        invoice.TotalAmount
    });
});



// DELETE ONE INVOICE
app.MapDelete("/api/invoices/{id:int}", async (
    int id,
    AppDbContext db) =>
{
    var invoice = await db.Invoices
        .FirstOrDefaultAsync(i => i.InvoiceId == id);

    if (invoice == null)
    {
        return Results.NotFound("Invoice not found.");
    }

    await using var transaction =
        await db.Database.BeginTransactionAsync();

    // Invoice items are deleted through the configured cascade delete
    db.Invoices.Remove(invoice);

    await db.SaveChangesAsync();
    await transaction.CommitAsync();

    return Results.Ok(new
    {
        Message = "Invoice deleted successfully.",
        InvoiceId = id
    });
});


// DELETE ALL INVOICES
// Requires ?confirm=true to reduce accidental deletion
app.MapDelete("/api/invoices", async (
    bool confirm,
    AppDbContext db) =>
{
    if (!confirm)
    {
        return Results.BadRequest(
            "To delete all invoices, use ?confirm=true."
        );
    }

    await using var transaction =
        await db.Database.BeginTransactionAsync();

    // Delete child records before parent records
    int deletedItems = await db.InvoiceItems.ExecuteDeleteAsync();
    int deletedInvoices = await db.Invoices.ExecuteDeleteAsync();

    await transaction.CommitAsync();

    return Results.Ok(new
    {
        Message = "All invoices deleted successfully.",
        DeletedInvoices = deletedInvoices,
        DeletedItems = deletedItems
    });
});


// SALES TRANSACTIONS REPORT
app.MapGet("/api/reports/sales", async (
    DateTime? fromDate,
    DateTime? toDate,
    int? customerId,
    AppDbContext db) =>
{
    if (fromDate.HasValue &&
        toDate.HasValue &&
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
            DateTime endDateExclusive =
                toDate.Value.Date.AddDays(1);

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

    decimal totalSalesAmount =
        transactions.Sum(t => t.TotalAmount);

    int totalInvoices = transactions.Count;

    return Results.Ok(new
    {
        Transactions = transactions,
        TotalSalesAmount = totalSalesAmount,
        TotalInvoices = totalInvoices
    });
});






// CREATE A NEW CUSTOMER
app.MapPost("/api/customers", async (
    CreateCustomerRequest request,
    AppDbContext db) =>
{
   
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(
            "Customer name is required.");
    }


    if (string.IsNullOrWhiteSpace(request.Email) ||
        string.IsNullOrWhiteSpace(request.Phone))
    {
        return Results.BadRequest(
            "Email and phone number are required.");
    }

    var customer = new Customer
    {
        Name = request.Name.Trim(),
        Email = request.Email.Trim(),
        Phone = request.Phone.Trim()
    };

    db.Customers.Add(customer);

    await db.SaveChangesAsync();

    return Results.Created(
        $"/api/customers/{customer.CustomerId}",
        new
        {
            Message = "Customer created successfully.",
            customer.CustomerId,
            customer.Name,
            customer.Email,
            customer.Phone
        });
});




 // DELETE A CUSTOMER
app.MapDelete("/api/customers/{id:int}", async (
    int id,
    AppDbContext db) =>
{
    var customer = await db.Customers
        .FirstOrDefaultAsync(c => c.CustomerId == id);

    if (customer == null)
    {
        return Results.NotFound("Customer not found.");
    }

    bool hasInvoices = await db.Invoices
        .AnyAsync(i => i.CustomerId == id);

    if (hasInvoices)
    {
        return Results.BadRequest(
            "Cannot delete this customer because they have existing invoices.");
    }

    db.Customers.Remove(customer);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        Message = "Customer deleted successfully.",
        CustomerId = id
    });
});



// GET ALL CUSTOMERS
app.MapGet("/api/customers", async (AppDbContext db) =>
{
    var customers = await db.Customers
        .OrderBy(c => c.Name)
        .Select(c => new
        {
            c.CustomerId,
            c.Name
        })
        .ToListAsync();

    return Results.Ok(customers);
});


// GET ALL PRODUCTS
app.MapGet("/api/products", async (AppDbContext db) =>
{
    var products = await db.Products
        .OrderBy(p => p.Name)
        .Select(p => new
        {
            p.ProductId,
            p.Name,
            p.Price
        })
        .ToListAsync();

    return Results.Ok(products);
});

app.Run();
