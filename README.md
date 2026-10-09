# CrossApp

## Getting Started

### Prerequisites

- .NET SDK 10.0
- Git

### Build & Run

```bash
dotnet build
dotnet run --project src/Cli -f net10.0 -- --demo
dotnet run --project src/Cli -f net10.0 -- data/sample.csv
```

#### Publish Size Comparison

| RID | Mode | Size publish | Runtime |
|-----|-------|-----------------|-------------------|
| linux-x64 | self-contained | 80M | no |
| linux-x64 | framework-dependent | 132K | yes (.NET 10) |
| win-x64 | self-contained | 77M | no |
| win-x64 | framework-dependent | 212K | yes (.NET 10) |

#### Self-contained publish bundles the entire .NET runtime together with the application code, so it takes up significantly more space, but it doesn't require .NET to be installed on the target machine — it can run out of the box.

#### Framework-dependent publish contains only the application's own code and dependencies, making it much smaller, but it requires a compatible .NET Runtime to already be installed on the user's machine.

---

#### Additional publish options Comparison

| RID | Mode | Size publish | Runtime |
|-----|-------|-----------------|---------|
| linux-x64 | self-contained + SingleFile | 74M | no |
| linux-x64 | self-contained + Trimmed | 24M | no |

### Multi-targeting Build Note (optional task)

`Core/EnvironmentInfo.cs` uses conditional compilation to show which
TFM the assembly was built for:

```csharp
#if NET10_0_OR_GREATER
    private const string BuildNote = "збірка під net10.0";
#else
    private const string BuildNote = "збірка під net9.0";
#endif
```

Verified with:

```bash
dotnet run --project src/Cli -f net9.0
dotnet run --project src/Cli -f net10.0
```

Each run prints a different `Примітка збірки` line, confirming the
conditional compilation works per-TFM.


## Environment

- .NET SDK 10.0
- Tested on: Omarchy 4.0.2 x64

## Project Structure

```
CrossApp/
├── .git/
├── .gitignore
├── CrossApp.slnx
├── README.md
└── src/
    ├── Core/
    │   ├── Core.csproj
    │   ├── EnvironmentInfo.cs
    │   ├── Dto/
    │   ├── Import/
    │   └── Domain/
    │       ├── Order.cs
    │       └── OrderLine.cs
    └── Cli/
        ├── Cli.csproj
        └── Program.cs
```

- `Core/Dto/` — data transfer record types (week 3): `ProductDto`,
  `CustomerDto`, `OrderDto`, `ImportResult<T>`
- `Core/Domain/` — entities with behavior and invariants (week 4)

## Lab 04: Orders domain

`Order` owns a collection of immutable `OrderLine` entities. Both types have
private constructors and validated `Create` factories. `AddLine` and `Confirm`
are the only operations that change an order. `Lines` exposes a read-only
wrapper, so callers cannot modify the internal list.

### Invariants

- Order ID and customer ID, name and email must not be blank (`ArgumentException`).
- Customer, DTO, DTO products collection and individual product DTOs must not be null (`ArgumentNullException`).
- Product ID, name and SKU must not be blank (`ArgumentException`). When creating a line without a separate SKU, its product ID is used as the SKU.
- Line quantity must be greater than zero (`ArgumentOutOfRangeException`).
- Product price must be non-negative (`ArgumentOutOfRangeException`); zero is allowed.
- Line totals must fit within `decimal` (`ArgumentOutOfRangeException`); adding a line must not overflow the order total (`InvalidOperationException`).
- An empty order cannot be confirmed (`InvalidOperationException`).
- A confirmed order cannot accept new lines or be confirmed again (`InvalidOperationException`).
- The total stored in an order DTO must equal the sum of its lines (`ArgumentException`).

Checks run before state changes. Exception messages are written in English.
`FromDto` restores an order through the same factories and domain checks,
including `Confirm` for a confirmed order.

### DTO mapping

The week 3 records remain data transfer types. `OrderLine.ToDto` maps a line to
`ProductDto`, preserving SKU and note. `Order.ToDto` preserves the customer,
products, calculated total and confirmation state. `OrderDto.IsConfirmed`
defaults to `false` for compatibility with older data that has no such field.
DTOs carry data; domain entities enforce business rules.

### CLI demonstration

From the `CrossApp` directory, run:

```bash
dotnet run --project src/Cli -f net10.0 -- --demo
# Alternatively, select the other supported framework:
dotnet run --project src/Cli -f net9.0 -- --demo
```

The success scenario creates an order, adds two lines, confirms it and restores
it through DTO mapping. Its total is `2850.00`.
The failure scenario demonstrates invalid input, empty-order confirmation,
changes after confirmation, repeated confirmation and a mismatched DTO total.
Each expected exception is caught and printed as its type and `Message`, without
a stack trace. The final output verifies that rejected operations left both
orders unchanged. The demonstration exits with code 1 if an expected rejection
does not occur or the checked state changes.

The existing CSV/JSON import is available by passing a file path:

```bash
dotnet run --project src/Cli -f net10.0 -- data/sample.csv
dotnet run --project src/Cli -f net10.0 -- data/sample.json
```

### Domain independence

`Core/Domain` depends on DTOs and standard in-memory types. It contains no console,
file I/O or CLI dependencies. Check this from `CrossApp`:

```bash
rg -n 'Console\.|File\.|System\.IO|Core\.Import|\bCli\b' src/Core/Domain
```

No matches are expected (ripgrep exits with code 1 when no match is found).
