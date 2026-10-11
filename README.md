# CrossApp

## Getting Started

### Prerequisites

- .NET SDK 10.0
- Git

### Build & Run

```bash
dotnet build
dotnet run --project src/Cli -f net10.0
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
    │   ├── Abstractions/IOrderStore.cs
    │   ├── Storage/
    │   ├── Services/OrderService.cs
    │   ├── SampleData.cs
    │   └── Domain/
    │       ├── Order.cs
    │       └── OrderLine.cs
    └── Cli/
        ├── Cli.csproj
        ├── Program.cs
        └── StoreFactory.cs
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
other orders. The service obtains the customer's orders through `IOrderStore`,
checks the cross-order rule and then calls `Order.Confirm`, which checks the
order's own invariants. This example assumes
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

## Lab 05: Service layer, storage and manual DI

### Contract and implementations

`Core/Abstractions/IOrderStore` is the storage contract:

| Method | Purpose |
|---|---|
| `List()` | Return a read-only list of orders with their lines |
| `GetById(id)` | Find an aggregate; return `null` when absent |
| `Add(order)` | Store a new aggregate; reject duplicate IDs |
| `Update(order)` | Replace an existing aggregate; reject an absent ID |
| `Remove(id)` | Delete an aggregate; return whether it existed |

IDs are trimmed and compared using `OrdinalIgnoreCase`. Both implementations
store snapshots and return detached entities, so editing an object returned by
`GetById` or `List` does not persist changes until `Update` is called.

- `InMemoryOrderStore`: a dictionary populated through an optional seed
  constructor. The CLI seeds 18 orders from `SampleData.Orders()`. Each new
  process starts with the same sample orders; user-created orders are lost.
- `FileOrderStore`: loads JSON on first access and keeps an in-memory cache.
  Each successful `Add`, `Update` or `Remove` persists the whole aggregate list
  as `OrderDto` records. Loading uses `Order.FromDto`, so invalid stored data
  cannot bypass domain rules. Writes use a temporary file in the same directory
  followed by replacement; the cache changes only after a successful write.
  A missing file starts an empty store. Duplicate IDs and malformed data are
  rejected rather than silently discarded.

The default file path is `data/orders.json` under `AppContext.BaseDirectory`,
normally `src/Cli/bin/Debug/net10.0/data/orders.json` when running .NET 10.
The CLI prints the absolute path. To share a stable file across frameworks,
pass `--file --path data/orders.json` from the project directory.
Existing sample CSV/JSON files are import inputs, not order-store files.

### Service and composition root

`OrderService` receives only `IOrderStore` through its constructor. Its business
operations are `CreateOrder`, `AddLine`, `Confirm` and `Cancel`. It also exposes
`All`, `Find`, `Remove` and predicate-based `Search`. Each mutation calls the
domain method and then persists the result. The five-confirmed-orders rule
from lab 04 now queries the injected store.

The main composition root is in `Program.cs`:

```csharp
IOrderStore store = options.UseFile
    ? new FileOrderStore(options.DataPath)
    : new InMemoryOrderStore(SampleData.Orders());
if (options.UseCache)
    store = new CachingOrderStore(store);
OrderService service = new(store);
```

Dependencies: `Cli -> OrderService -> IOrderStore`, with either
`InMemoryOrderStore` or `FileOrderStore` implementing the contract.
The service does not create concrete stores and has no console or file I/O.
No `Microsoft.Extensions.DependencyInjection` package or DI container is used.
When a database implementation is added later, it will implement `IOrderStore`
and be selected in the composition root. Domain entities and service business
operations will not need to depend on that implementation.

### Demonstration

Run from `CrossApp`; choose `-f net9.0` instead if needed:

```bash
# In-memory store: each run starts with 18 sample orders and ends with 19.
dotnet run --project src/Cli -f net10.0
dotnet run --project src/Cli -f net10.0

# File store: the second run sees the first run's order and adds one more.
dotnet run --project src/Cli -f net10.0 -- --file --path data/orders.json
dotnet run --project src/Cli -f net10.0 -- --file --path data/orders.json
```

The scenario prints the chosen store and initial record count, creates an order,
adds two lines, confirms it, finds it by ID and lists orders. It also searches
confirmed orders with totals of at least 1000, demonstrates duplicate and
missing-ID failures, and creates/removes a temporary order. The final count
must be the initial count plus one. A unique demo customer is used on each run
so repeated file demonstrations do not hit the customer's five-order limit.
The JSON file contains all order fields and lines, including status and notes.
Expected failures print only exception type and `Message` in English.

### Optional tasks

1. `CachingOrderStore` is a decorator receiving another `IOrderStore` through
   its constructor. It caches list and ID lookups, including absent IDs.
   Successful writes invalidate both caches. Returned objects are detached.
   `CacheHits` and `CacheMisses` make repeated reads visible in the CLI.
2. `OrderService.Search(Func<Order, bool> predicate)` supports arbitrary filters
   without adding file-specific operations to the storage contract. The CLI
   demonstrates `order => order.IsConfirmed && order.Total >= 1000m`.
3. `Cli/StoreFactory.Create(args)` creates the same store configuration when
   `--factory` is supplied. This moves construction and optional decoration
   into a dedicated CLI factory; the default path keeps the explicit ternary
   choice in `Program.cs`. Both use manual constructor injection. The factory
   is part of the CLI composition root, not a dependency of `OrderService`.

```bash
# Decorator and predicate search
dotnet run --project src/Cli -f net10.0 -- --cache

# Factory with memory storage
dotnet run --project src/Cli -f net10.0 -- --factory --cache

# Factory with file storage and caching
dotnet run --project src/Cli -f net10.0 -- --factory --file --cache --path data/orders.json
```

Storage instances are intended for sequential use by one CLI process.
File caches do not monitor external writes; the decorator assumes all changes
go through it. Concurrent processes or database transactions are outside this
lab's implementation.

### Show lab 04 followed by lab 05

```bash
dotnet run --project src/Cli -f net10.0 -- --demo
dotnet run --project src/Cli -f net10.0 -- --extras-demo
dotnet run --project src/Cli -f net10.0
dotnet run --project src/Cli -f net10.0 -- --file --path data/orders.json
```

The lab 04 modes use their own objects and never write to the lab 05 data file.
CSV/JSON import remains available by passing a single input file path.

### Checks

The package-free checks in `tests/Lab05Checks` verify the shared store contract,
detached reads, service invariants, persistence and reload, invalid-file handling,
failed-write behavior, cache hits/invalidation and factory options:

```bash
dotnet run --project tests/Lab05Checks -f net10.0
dotnet run --project tests/Lab05Checks -f net9.0
rg -n 'Console\.|File\.|System\.IO' src/Core/Domain src/Core/Services
rg -n 'FileOrderStore|InMemoryOrderStore|CachingOrderStore' src/Core/Services
```

Both searches should return no matches. Tests use temporary directories and
clean them up without touching demonstration data.
