# Sales Invoice API

A RESTful backend application built using ASP.NET Core Minimal API, Entity Framework Core, and Microsoft SQL Server.

The application manages sales invoices, customers, products, discounts, and sales reports. It provides the backend services for the [SalesInvoiceUI](https://github.com/RamyBarakat01/SalesInvoiceUI) Blazor application.

## Features

### Invoice Management
- Create sales invoices containing one or more products.
- Validate customer IDs, product IDs, quantities, prices, and discounts.
- Retrieve invoice details by ID.
- Update existing invoices, including product quantities and unit prices.
- Add and remove products from existing invoices.
- Delete individual invoices and their associated invoice items.
- Delete all invoices using an explicit confirmation query parameter.
- Generate unique invoice numbers.
- Save invoice operations using database transactions.

### Discount System
- Apply percentage-based discounts to individual invoice items.
- Apply an additional percentage-based discount to the entire invoice.
- Automatically calculate line totals, subtotals, discounts, and final invoice totals.
- Validate discount percentages between 0% and 100%.
- Preserve existing discount values when they are omitted from invoice update requests.

### Automatic Rounding Discount
- Automatically round final invoice totals down to the nearest whole Egyptian pound (EGP).
- Record the fractional amount removed as a separate rounding discount.
- Apply rounding when creating invoices and updating existing invoices.
- Store the final rounded amount in the database.

For example:

| Description | Amount |
|---|---:|
| Subtotal after item discounts | EGP 1,150.00 |
| Invoice discount (5%) | - EGP 57.50 |
| Amount before rounding | EGP 1,092.50 |
| Automatic rounding discount | - EGP 0.50 |
| Final invoice total | **EGP 1,092.00** |

Existing invoices are not automatically recalculated during database migrations. Their totals are recalculated when updated.

### Customer Management
- Retrieve a list of customers.
- Create new customers.
- Delete customers who have no existing invoices.
- Prevent deletion of customers associated with existing invoices.

### Sales Reporting
- Retrieve sales transactions.
- Filter reports by date range and customer.
- Calculate total sales amount and invoice count.
- Use saved final invoice totals, including applied discounts and rounding adjustments.

## Technologies Used

- C#
- .NET 10
- ASP.NET Core Minimal API
- Entity Framework Core
- Microsoft SQL Server
- SQL Server Management Studio (SSMS)
- Postman for API testing
- Git and GitHub for version control

## API Endpoints

### Invoice Endpoints

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/invoices` | Basic API welcome endpoint |
| POST | `/api/invoices` | Create an invoice with optional discounts |
| GET | `/api/invoices/{id}` | Retrieve invoice details, including discounts |
| PUT | `/api/invoices/{id}` | Update items, quantities, prices, and discounts |
| DELETE | `/api/invoices/{id}` | Delete a specific invoice |
| DELETE | `/api/invoices?confirm=true` | Delete all invoices and their items |

**Warning:** The delete-all endpoint permanently removes all saved invoices and invoice items. The confirmation parameter is a safeguard against accidental requests, not an authentication mechanism.

### Customer and Product Endpoints

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/customers` | Retrieve customers |
| POST | `/api/customers` | Create a customer |
| DELETE | `/api/customers/{id}` | Delete a customer without existing invoices |
| GET | `/api/products` | Retrieve available products and prices |

### Reporting Endpoint

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/reports/sales` | Retrieve sales transactions and totals with optional filters |

## Example: Create Invoice

**POST** `/api/invoices`

```json
{
  "customerId": 1,
  "discountPercent": 5,
  "items": [
    {
      "productId": 1,
      "quantity": 2,
      "discountPercent": 15
    },
    {
      "productId": 2,
      "quantity": 1,
      "discountPercent": 0
    }
  ]
}
```

The API retrieves product prices from SQL Server, calculates each item's discount, applies the invoice-level discount, and automatically rounds the final amount down to the nearest whole EGP.

Discount percentages are optional and default to 0% when creating an invoice.

## Example: Update Invoice

**PUT** `/api/invoices/16`

```json
{
  "discountPercent": 5,
  "items": [
    {
      "invoiceItemId": 29,
      "quantity": 2,
      "discountPercent": 15
    },
    {
      "invoiceItemId": 30,
      "quantity": 1,
      "discountPercent": 0
    }
  ]
}
```

Existing items can have their quantities, unit prices, and discounts updated.

To add a new product, supply an `invoiceItemId` of `0` together with its `productId` and `quantity`. New products use their price from the database.

To remove a product, omit its existing invoice item from the request.

An invoice must retain at least one item.

The API recalculates the invoice total after every successful update.

## Sales Report Filtering

The reporting endpoint supports optional query parameters:

- `fromDate` — Starting date.
- `toDate` — Ending date (inclusive).
- `customerId` — Filter by customer.

Example:

`GET /api/reports/sales?fromDate=2026-10-01&toDate=2026-10-31&customerId=1`

The response contains matching transactions, the total sales amount, and the total invoice count.

## Database

The application uses four main tables:

| Table | Purpose |
|---|---|
| Customers | Stores customer information |
| Products | Stores product information and unit prices |
| Invoices | Stores invoice headers, invoice-level discounts, rounding discounts, and final totals |
| InvoiceItems | Stores products, quantities, prices, item discounts, and discounted line totals |

A customer can have multiple invoices. Each invoice can contain multiple invoice items, and each invoice item references a product.

Entity Framework Core handles database access and schema migrations.

### Included Migrations

- `InitialCreate` — Creates the initial database tables.
- `AddInvoiceDiscounts` — Adds percentage discount fields.
- `AddRoundingDiscount` — Adds the automatic rounding discount field.

## Getting Started

1. Install the .NET 10 SDK and Microsoft SQL Server.
2. Clone the repository:

   ```bash
   git clone https://github.com/RamyBarakat01/SalesInvoiceAPI.git
   ```

3. Navigate to the project directory:

   ```bash
   cd SalesInvoiceAPI
   ```

4. Configure the `DefaultConnection` connection string for your SQL Server instance. Do not commit database credentials or passwords to GitHub.
5. Restore dependencies:

   ```bash
   dotnet restore
   ```

6. Apply the included database migrations:

   ```bash
   dotnet ef database update
   ```

   The `dotnet ef` command requires the EF Core CLI tool to be installed.

7. Add initial customer and product records to the database. Customers can also be created through the API.
8. Start the application:

   ```bash
   dotnet run
   ```

9. Test the API endpoints using Postman.

The application runs locally and requires a working SQL Server connection.

## Testing

The following functionality has been implemented and manually tested during development:

- Invoice creation and retrieval.
- Customer and product validation.
- Updating invoice quantities and unit prices.
- Adding and removing invoice products.
- Deleting individual invoices.
- Creating and deleting customers.
- Preventing deletion of customers with existing invoices.
- Percentage discounts for items and invoices.
- Discount recalculation when invoices are updated.
- Automatic rounding discounts.
- Retrieving saved rounding discounts.
- Sales reporting and filtering.
- Invalid or empty invoice items.
- Nonexistent invoice IDs.
- Invalid date ranges.

The delete-all confirmation flow was tested, but executing a complete deletion of all invoice records was intentionally avoided.

Successful database transaction commits were verified. Failure-triggered rollback was not separately tested.

## Frontend Application

The frontend is a separate Blazor Web App that communicates with this API using HTTP requests.

Repository: [SalesInvoiceUI](https://github.com/RamyBarakat01/SalesInvoiceUI)

## Development Status

This is a locally developed and tested project. It has not been configured for public production deployment. Authentication, authorization, and production security controls would need to be addressed before exposing the API publicly.
