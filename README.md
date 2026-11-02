# CrossApp

## Lab 08: async import, cancellation and Core tests

The order variant imports a JSON array of complete orders with their product
lines (`OrderDto`), preserving the aggregate and verifying its calculated total.
The old single-file CSV/product demonstration remains available.

Run from `CrossApp`:

```bash
dotnet build -m:1
dotnet test -m:1
dotnet test -m:1  # repeat to check isolation
dotnet test -m:1 --collect:"XPlat Code Coverage" --results-directory TestResults/lab08

dotnet run --project src/Cli -f net10.0 -- --import-orders data/lab08-orders.json
dotnet run --project src/Cli -f net10.0 -- --import-orders data/lab08-orders.json --stream
dotnet run --project src/Cli -f net10.0 -- --import-orders data/lab08-invalid.json

# Deterministic cancellation, including on a small file:
dotnet run --project src/Cli -f net10.0 -- --import-orders data/lab08-orders.json --timeout-ms 0

# Persist imported orders. The second invocation adds zero records.
dotnet run --project src/Cli -f net10.0 -- --import-orders data/lab08-orders.json --file --path /tmp/lab08-orders.json
```

Use Ctrl+C during a large import, or pass `--timeout-ms 2000`. Both signal the
linked token through `Cli -> OrderService -> JsonOrderImporter -> JSON/file I/O`
and the loops converting orders and product lines. Cancellation prints
`Імпорт скасовано.` and exits with code 2; rejected records and technical errors
exit with code 1; success exits with code 0. `--timeout-ms 0` cancels immediately,
whereas positive values use `CancelAfter`. No artificial delays or `Task.Run`
are used in production. The timeout test waits until the fake importer starts,
then arms a real timer and verifies that the user token remains active.

`IOrderImporter.LoadAsync` uses `DeserializeAsync` and an asynchronous
`FileStream`. The buffered service validates the full input before adding it.
`StreamAsync` uses `DeserializeAsyncEnumerable` and `[EnumeratorCancellation]`;
`ImportStreamAsync` consumes it with `await foreach`. This avoids retaining the
whole input list, although the in-memory destination still retains all imported
orders. Streaming can leave successfully added records if a later record is
invalid. Both modes retain completed writes on cancellation; neither promises
a transaction. Repeating an import skips existing IDs case-insensitively and
emits no duplicate events. Imported confirmed orders also obey the customer limit.

The buffered mode reports percentages via `IProgress<int>` for the insertion
phase, including skipped duplicates. It does not claim to measure JSON-reading
time. Streaming prints the final imported count; no total record count is known
in advance. Console progress is delivered synchronously to keep output ordered.
Changed events fire once per added aggregate after storage succeeds.

Business-rule failures now use `Core.DomainException`. Invalid method arguments
still use `ArgumentException` and its subclasses. Import validation wraps invalid
DTO data with a record number and preserves `InnerException`; `JsonException`,
I/O errors and cancellation propagate with their original types.
`IOrderStore` and LINQ reports remain synchronous; file-store writes retain their
existing synchronous contract.

`tests/Core.Tests` targets .NET 10 and references only Core. Its fixed data is
local to the tests, with fresh in-memory stores and no dependency on `SampleData`
or repository data. There are 23 cases, including theories, event/state checks,
deterministic reports, idempotence, cancellation during insertion, timeout origin,
progress and JSON validation. Only one test uses a temporary file, deleted in
`finally`. Coverlet is configured for the command above.

Validation in this restricted workspace: build passed for .NET 9 and .NET 10;
all 23 cases passed twice using a supplemental in-process invocation of the same
xUnit tests. VSTest's normal `dotnet test` and coverage collection were blocked
by denied local sockets (`SocketException: Permission denied`). This is not a
successful standard test run. Coverlet was additionally run directly against the supplemental runner: Core line
coverage is 45.92% (327/712), branch coverage 44.34% (98/221). The new import
methods have 100% line coverage. This is a separate measurement, not a successful
`--collect` run. To reproduce it after restoring the test packages:
`python ../Reports/lab8/verification/verify.py`. Details and actual output:
[lab 08 report](../Reports/lab8/lab8.md).

## Lab 07: LINQ reports

Reports use the order variant of the lab: top customers by total order value,
orders above a threshold, grouping by status, average order value and the
largest order. `Core/Reports/ReportService` accepts `IEnumerable<Order>` and
returns immutable record DTOs in materialized read-only lists. `Totals` returns
an explicitly named tuple with decimal amounts. Reports include all statuses,
including cancelled orders; totals describe this dataset, not paid revenue.
All filtering, projection, grouping and sorting stay in Core. CLI only parses
options, invokes reports and formats their results.

Run from `CrossApp` (both `net9.0` and `net10.0` are supported):

```bash
dotnet build -m:1 -p:BuildInParallel=false
dotnet run --no-build --project src/Cli -f net10.0 -- report-top
dotnet run --no-build --project src/Cli -f net10.0 -- report-high --threshold 1000
dotnet run --no-build --project src/Cli -f net10.0 -- report-groups
dotnet run --no-build --project src/Cli -f net10.0 -- report-sum
dotnet run --no-build --project src/Cli -f net10.0 -- report-low --threshold 1000
dotnet run --no-build --project src/Cli -f net10.0 -- report-orders --take 5
dotnet run --no-build --project src/Cli -f net10.0 -- report-deferred
```

| Command | Core method | Result |
|---|---|---|
| `report` / `report-top` | `TopCustomers` | Top N customers by sum, then unique customer ID |
| `report-orders` | `TopOrders` | Top N orders by total, then unique order ID |
| `report-high` | `AboveTotal` | Orders strictly above threshold, ascending amount and ID |
| `report-low` | `BelowTotal` | Orders strictly below threshold, ascending amount and ID |
| `report-groups` | `ByStatus` | Count and sum by status, descending sum and ordinal status name |
| `report-sum` | `Totals` | Count, sum, average and maximum; zeros for empty input |
| `report-deferred` | `DemonstrateDeferred` | Deferred query versus materialized DTO snapshot |

The existing dataset already contains 18 orders, six customers and three
statuses, so it meets this variant's prerequisite. With the default threshold
of 1000, 11 orders are below it and six are above it; an order worth exactly
1000 belongs to neither strict filter. Totals are 18 orders, 16800.00 overall,
933.33 average and 2550.00 maximum. `SampleData.Customers()` also includes a
seventh customer without orders for the optional `GroupJoin` example.
Top reports use ordinal unique IDs to break ties independently of input order.

Storage options can precede or follow the command. The parser consumes the
value after `--path`, so a file path cannot be mistaken for a report command:

```bash
dotnet run --no-build --project src/Cli -f net10.0 -- --file --path /tmp/lab07-orders.json report-sum
dotnet run --no-build --project src/Cli -f net10.0 -- report-top --factory --cache
```

A missing storage file represents an empty dataset: all report lists are empty,
and `report-sum` prints four zeros. Report commands only read storage and never
create orders or emit change events. `--take` accepts a nonnegative integer
(default 5); `--threshold` accepts a nonnegative decimal using `.` (default 1000).
The deferred demo requires a positive threshold. Invalid commands/options
return exit code 1. Running without a report command preserves earlier modes.

### Deferred execution

Building `Where` stores a query; filtering begins at each `Count()` or `ToList()`.
The demo copies the store snapshot to a local list, counts 11 matching orders,
adds a zero-value draft, and counts again: the same query now returns 12.
Projecting into DTOs and calling `ToList()` fixes a snapshot of 12 rows, which
remains unchanged after the local source is cleared, while a new query count
returns zero. Public report methods materialize results before returning them,
so printing or reusing a report never repeats its query.

### Optional tasks

```bash
dotnet run --no-build --project src/Cli -f net10.0 -- report-join
dotnet run --no-build --project src/Cli -f net10.0 -- report-query
dotnet run --no-build --project src/Cli -f net10.0 -- report-benchmark
dotnet run --no-build --project src/Cli -f net10.0 -- --lab07-extras-demo
```

1. `CustomersWithOrders` uses `GroupJoin` between the supplied customer directory
   and orders. It returns `CustomerSummary` DTOs and keeps customers with zero
   matching orders, including `C-007`. `report-join` uses the sample directory;
   orders for customer IDs outside that directory do not appear in this join.
2. `ReportExtensions.TopBy<T>` implements descending ranking and ordinal ID
   tie-breaking. Both `TopCustomers` and `TopOrders` use it before DTO results
   are materialized. Negative N is rejected; zero returns an empty report.
3. `ByStatusQuery` expresses grouping and projection through
   `from ... group ... into ... select`, then applies the same explicit ordinal
   sorting as the method form. Query syntax highlights groups; method syntax
   makes filtering, sorting and `Take` easier to follow as a chain.
   `report-query` also prints whether both results match.
4. `Benchmark` measures the complete filter/top-N/projection on 100000 generated
   orders with and without a `ToList` between filtering and sorting. Input
   generation and the initial input snapshot are outside the measured section.
   Both variants are warmed up, run five times with alternating first runner,
   and report mean milliseconds and result equality. The intermediate list adds
   an allocation; observed timing depends on the runtime, GC and system load
   and does not establish that one variant is always faster.

No test project is required by lab 07; temporary validation stays outside the
repository. No separate lab report or commit is created for this lab.

## Lab 06: Domain events, delegates and lambdas

The order domain follows the lab's order variant. `OrderChangeKind` describes
creation, line addition, confirmation, cancellation and removal.
`OrderChangedEventArgs` carries `OrderId`, `LinesCount`, `Total`, `Status` and `At`.
`OrderService.Changed` is an `event EventHandler<OrderChangedEventArgs>`.
Every successful service mutation raises it once, after persistence succeeds.
Failed invariants, failed writes and removal of an absent order raise no event.
Removal arguments describe the last state of the deleted order.

Run the seven-step demonstration from `CrossApp`:

```bash
dotnet build -m:1 -p:BuildInParallel=false
dotnet run --no-build --project src/Cli -f net10.0 -- --events-demo
dotnet run --no-build --project src/Cli -f net9.0 -- --events-demo
dotnet run --no-build --project src/Cli -f net10.0 -- --events-demo --file --path /tmp/lab06-orders.json
dotnet run --no-build --project src/Cli -f net10.0 -- --events-demo --factory --cache
```

The first subscriber is a local method writing to the console and
`Path.Combine(AppContext.BaseDirectory, "logs", "app.log")`; the directory is
created by the CLI. The second is a stored lambda capturing `changes`.
The demo verifies their subscription order. Creation, two lines and confirmation
produce four notifications. After the first subscriber is removed, cancellation
changes the stored status and increments the lambda counter to five while the
log still contains four new lines. Both handlers are then attached again for
the failure scenarios: negative quantity, missing ID and empty confirmation
emit no events. `FindAll(order => order.Total < 1000m)` demonstrates `Func`.
The temporary draft is removed at the end; its creation and removal add two
further log entries. Log files append across runs; each run adds six entries.
The created cancelled order remains in the selected store.

The CLI catches file I/O errors inside the subscriber, reports them and returns
a failed self-check without throwing back into an already completed mutation.
Events are synchronous: an uncaught subscriber exception would interrupt the
remaining handlers after the state was persisted. The service has no console
or log-file dependency. Existing `Search` delegates to `FindAll`.

The CLI demonstration checks counts, handler order, unsubscription, invariants
and predicate search, and returns a nonzero exit code when a check fails:

```bash
dotnet run --no-build --project src/Cli -f net10.0 -- --events-demo
rg -n 'Console|logs' src/Core
```

The search should return no matches. The step-by-step Ukrainian report,
actual console output and sample log entries are in
[../Reports/lab6/lab6.md](../Reports/lab6/lab6.md).

### Optional tasks

```bash
dotnet run --no-build --project src/Cli -f net10.0 -- --lab06-extras-demo
dotnet run --no-build --project src/Cli -f net9.0 -- --lab06-extras-demo
```

1. `Core/Abstractions/INotificationSink` exposes `Notify(OrderChangedEventArgs)`.
   CLI implementations `ConsoleSink` and `FileSink` are combined through
   `CompositeNotificationSink` and passed to `OrderService(store, sink)` by manual
   constructor injection. The optional constructor argument defaults to `null`;
   the original event demonstration uses only events. Both mechanisms run after
   persistence; the sink runs before event handlers. Removing an event handler
   leaves the injected sink active, which the demo verifies through four new
   lines in `logs/sinks.log` and an event counter that stays at three.
   Core knows the interface, while concrete console and file operations remain
   in CLI. A throwing sink propagates its exception after persistence and prevents
   event dispatch, just as an uncaught event-handler exception interrupts dispatch.
2. `ProductCsvImporter.Load(path, Action<string>? onSkipped = null)` invokes the
   callback once per invalid data row and also retains each error in `ImportResult`.
   Headers, comments and blank rows are skipped without error notifications.
   The demo imports one valid row, reports three invalid rows through a lambda
   and checks that calls with and without a callback return the same results.
   Callback exceptions propagate; callers control the callback implementation.
3. The report explains why an async handler matching `EventHandler<T>` is
   `async void`, why its completion cannot be awaited by `Invoke`, and why
   exceptions must be handled inside it. It includes a guarded example and the
   alternative of an explicitly awaited `Func<..., Task>`.
   See [Microsoft's async return types documentation](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/async-return-types).

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
- Line totals must fit within `decimal` (`ArgumentOutOfRangeException`); adding a line must not overflow the order total (`DomainException`).
- An empty order cannot be confirmed (`DomainException`).
- A confirmed order cannot accept new lines or be confirmed again (`DomainException`).
- A cancelled order cannot accept new lines, be confirmed or be cancelled again (`DomainException`).
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
reached, it throws `DomainException` without changing the new order.

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

All other transitions throw `DomainException` (lab 08). Only draft orders
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
