# Sales Invoice API

A RESTful backend application built using ASP.NET Core, Entity Framework Core, and SQL Server.

## Features

- Create sales invoices with one or more products.
- Validate customer and product IDs.
- Automatically calculate line totals and invoice totals.
- Generate unique invoice numbers.
- Save invoices and invoice items using database transactions.
- Retrieve invoice details by ID.
- Generate sales reports filtered by date range and customer.
- Calculate total sales amount and total invoice count.

## Technologies Used

- C#
- .NET 10 / ASP.NET Core Minimal API
- Entity Framework Core
- Microsoft SQL Server
- Postman for API testing

## API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/invoices` | Create a sales invoice |
| GET | `/api/invoices/{id}` | Retrieve invoice details |
| GET | `/api/reports/sales` | Retrieve sales transactions and totals |

## Example: Create Invoice

**POST** `/api/invoices`

```json
{
  "customerId": 1,
  "items": [
    {
      "productId": 1,
      "quantity": 2
    },
    {
      "productId": 2,
      "quantity": 1
    }
  ]
}
```

The API retrieves product prices from the database and calculates the totals automatically.

## Sales Report Filtering

The reporting endpoint supports optional query parameters:

- `fromDate` — Starting date
- `toDate` — Ending date (inclusive)
- `customerId` — Filter by customer

Example:

`GET /api/reports/sales?fromDate=2026-10-01&toDate=2026-10-31&customerId=1`

The response contains the matching invoices, total sales amount, and total invoice count.

## Database

The application uses four main tables:

- Customers
- Products
- Invoices
- InvoiceItems

Entity Framework Core migrations are included in the repository.

## Getting Started

1. Install the .NET 10 SDK and SQL Server.
2. Clone this repository.
3. Configure the `DefaultConnection` string in `appsettings.json` for your local SQL Server instance.
4. Run `dotnet restore`.
5. Run `dotnet ef database update` to create the database schema.
6. Add customer and product records to the database.
7. Run `dotnet run` to start the application.
8. Test the API endpoints using Postman.

## Testing

The following scenarios have been tested:

- Successful invoice creation and calculations
- Generation of distinct invoice numbers
- Customer and product validation
- Invalid or empty invoice items
- Retrieval of existing and nonexistent invoices
- Sales reporting with optional filters
- Empty report results
- Invalid date ranges

Successful transaction commits were verified. Failure-triggered rollback was not separately tested.
