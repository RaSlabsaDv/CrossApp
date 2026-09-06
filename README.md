# CrossApp

A cross-platform programming course project, built incrementally across 16 lab assignments.

## Domain: Orders

CrossApp manages customers, product catalog, and order processing.

### Entities

- **Customer** — a person or company placing orders (name, contact info).
- **Category** — a product category used to organize the catalog (e.g. Electronics, Groceries).
- **Product** — an item available for purchase, belonging to a Category (name, price, SKU).
- **Order** — a purchase made by a Customer, containing one or more order lines, with a status and date.
- **OrderLine** — a single line item within an Order, referencing a Product, quantity, and unit price.

### Purpose

The application supports placing customer orders, organizing products by category, and calculating order totals based on order lines.

## Getting Started

### Prerequisites

- .NET SDK 10.0
- Git

### Build & Run

```bash
dotnet build
dotnet run --project src/Cli
```

### Publish (self-contained)

```bash
dotnet publish src/Cli -c Release -r <RID> --self-contained true
```

Replace `<RID>` with your target runtime identifier (e.g. `win-x64`, `linux-x64`, `osx-arm64`).

## Environment

- .NET SDK 10.0
- Tested on: Omarchy 4.0.2 x64

## Project Structure

```
CrossApp/
├── .git/
├── .gitignore
├── CrossApp.sln
├── README.md
└── src/
    └── Cli/
        ├── Cli.csproj
        └── Program.cs
```