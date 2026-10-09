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
private constructors and validated `Create` factories. `AddLine`, `Confirm` and `Cancel`
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
- A cancelled order cannot accept new lines, be confirmed or be cancelled again (`InvalidOperationException`).
- The total stored in an order DTO must equal the sum of its lines (`ArgumentException`).
- An explicit DTO status must be known and agree with `IsConfirmed` (`ArgumentException`).

Checks run before state changes. Exception messages are written in English.
`FromDto` restores an order through the same factories and domain checks,
including `Confirm` for a confirmed order.

### DTO mapping

The week 3 records remain data transfer types. `OrderLine.ToDto` maps a line to
`ProductDto`, preserving SKU and note. `Order.ToDto` preserves the customer,
products, calculated total and status. `OrderDto.IsConfirmed`
defaults to `false` for compatibility with older data that has no such field.
The optional string `Status` stores `Draft`, `Confirmed` or `Cancelled`;
when absent, the state is restored using the older `IsConfirmed` field.
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

## Lab 04: Optional tasks

### 1. Import DTOs into domain entities

`DomainImporter.ToOrderLines` takes `ImportResult<ProductDto>` and returns
`ImportResult<OrderLine>`. `ToOrders` similarly converts `ImportResult<OrderDto>`
to `ImportResult<Order>`. Existing parsing errors are preserved; each DTO that
violates domain rules adds an English error message while other DTOs continue
to be processed. The resulting collections are read-only.

Errors identify the 1-based record position within `ImportResult.Items`.
This is not necessarily a source-file line number: the week 3 import result
does not retain source positions for accepted DTOs. Existing parser errors
keep their original source-line information.

CSV/JSON CLI imports now also pass accepted DTOs through domain validation and
print the accepted entity count and any additional domain errors.

### 2. A rule spanning multiple entities

`OrderService.Confirm` checks that the customer has fewer than five confirmed
orders before confirming another one. It counts distinct order IDs belonging
to that customer; draft and cancelled orders do not count. When the limit is
reached, it throws `InvalidOperationException` without changing the new order.

The rule belongs in a service because one `Order` does not know the customer's
other orders. The caller supplies the full current collection of relevant
orders. The service checks the cross-order rule and then calls `Order.Confirm`,
which checks the order's own invariants. A future repository-backed service
will obtain that collection from storage. This in-memory example assumes
sequential operations; it does not coordinate concurrent confirmations.

### 3. Explicit order states

`OrderStatus` contains `Draft`, `Confirmed` and `Cancelled`. `ChangeStatus` checks
allowed transitions with a switch expression before changing the private state:

| Current state | Operation | New state |
|---|---|---|
| Draft, with at least one line | Confirm | Confirmed |
| Draft | Cancel | Cancelled |
| Confirmed | Cancel | Cancelled |

All other transitions throw `InvalidOperationException`. Only draft orders
accept new lines. `IsConfirmed` is calculated from `Status`, so there are no
independent state fields that can disagree. DTO mapping preserves all three
states, including empty cancelled orders.

### Run the optional tasks

```bash
dotnet run --project src/Cli -f net10.0 -- --extras-demo
dotnet run --project src/Cli -f net9.0 -- --extras-demo
```

The demonstration shows partial import with errors, rejection of a sixth
confirmed order, confirmation after another order is cancelled, allowed and
forbidden state transitions, and DTO restoration of a cancelled order.
It exits with code 1 if the checked behavior is incorrect.
